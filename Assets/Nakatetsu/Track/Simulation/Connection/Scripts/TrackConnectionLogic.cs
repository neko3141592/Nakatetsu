using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Connection;

namespace Nakatetsu.Track.Simulation.Connection
{
    public static class TrackConnectionLogic
    {
        public static bool TryInitialize(TrackConnectionContext context, TrackGraphContext graph,
            TrackSwitchPosition initialPosition, out string error)
        {
            context.Clear();
            if (graph == null || !graph.IsInitialized)
                return Fail("Track graph is not initialized.", out error);
            if (!IsConfirmedPosition(initialPosition))
                return Fail("Initial switch position must be Normal or Reverse.", out error);

            foreach (var definition in graph.Connections)
            {
                if (!TrackConnectionValidator.TryValidate(definition, graph, out error))
                {
                    context.Clear();
                    return false;
                }
                var snapshot = new TrackConnectionDefinition
                {
                    connectionId = definition.connectionId,
                    nodeId = definition.nodeId
                };
                foreach (var pair in definition.edgePairs)
                    snapshot.edgePairs.Add(new TrackEdgePair
                    {
                        pairId = pair.pairId,
                        edgeAId = pair.edgeAId,
                        edgeBId = pair.edgeBId,
                        condition = pair.condition
                    });
                context.ConnectionsByNodeId.Add(snapshot.nodeId, snapshot);
                if (snapshot.edgePairs.Count == 2)
                    context.StatesById.Add(snapshot.connectionId,
                        new TrackConnectionState(initialPosition, initialPosition, false));
            }

            context.IsInitialized = true;
            error = null;
            return true;
        }

        /// <summary>連動装置等からの要求。受付だけで位置確定にはしない。</summary>
        public static bool TryRequestPosition(TrackConnectionContext context, string connectionId,
            TrackSwitchPosition position, out string error)
        {
            if (!IsConfirmedPosition(position))
                return Fail("Requested position must be Normal or Reverse.", out error);
            if (!context.TryGetState(connectionId, out var state))
                return Fail($"Switch '{connectionId}' is unavailable or not initialized.", out error);
            if (state.IsMoving && state.RequestedPosition != position)
                return Fail($"Switch '{connectionId}' is already moving to another position.", out error);

            if (!state.IsMoving && state.ActualPosition != position)
                context.StatesById[connectionId] =
                    new TrackConnectionState(position, TrackSwitchPosition.Unknown, true);

            error = null;
            return true;
        }

        /// <summary>転換動作側からの完了通知。要求と一致した位置だけを確定する。</summary>
        public static bool TryConfirmPosition(TrackConnectionContext context, string connectionId,
            TrackSwitchPosition position, out string error)
        {
            if (!IsConfirmedPosition(position))
                return Fail("Confirmed position must be Normal or Reverse.", out error);
            if (!context.TryGetState(connectionId, out var state))
                return Fail($"Switch '{connectionId}' is unavailable or not initialized.", out error);
            if (state.RequestedPosition != position)
                return Fail($"Confirmation for '{connectionId}' does not match the requested position.", out error);

            context.StatesById[connectionId] = new TrackConnectionState(position, position, false);
            error = null;
            return true;
        }

        /// <summary>確定した実際の位置から、双方向の接続ペアの相手Edgeを返す。</summary>
        public static bool TryResolveNextEdge(TrackConnectionContext context, string nodeId,
            string incomingEdgeId, out string nextEdgeId, out string error)
        {
            nextEdgeId = null;
            if (!context.IsInitialized)
                return Fail("Track connections are not initialized.", out error);
            if (string.IsNullOrWhiteSpace(nodeId) ||
                !context.ConnectionsByNodeId.TryGetValue(nodeId, out var connection))
                return Fail($"Node '{nodeId}' has no connection definition.", out error);

            var condition = TrackConnectionCondition.Always;
            if (connection.edgePairs.Count == 2)
            {
                if (!context.TryGetState(connection.connectionId, out var state) || state.IsMoving ||
                    !IsConfirmedPosition(state.ActualPosition))
                    return Fail($"Switch '{connection.connectionId}' has no confirmed position.", out error);
                condition = state.ActualPosition == TrackSwitchPosition.Normal
                    ? TrackConnectionCondition.Normal : TrackConnectionCondition.Reverse;
            }

            foreach (var pair in connection.edgePairs)
            {
                if (pair.condition != condition) continue;
                if (pair.edgeAId == incomingEdgeId) nextEdgeId = pair.edgeBId;
                else if (pair.edgeBId == incomingEdgeId) nextEdgeId = pair.edgeAId;
                if (nextEdgeId != null)
                {
                    error = null;
                    return true;
                }
            }
            return Fail($"Edge '{incomingEdgeId}' is not connected through Node '{nodeId}' in the current position.", out error);
        }

        private static bool IsConfirmedPosition(TrackSwitchPosition position) =>
            position == TrackSwitchPosition.Normal || position == TrackSwitchPosition.Reverse;

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
