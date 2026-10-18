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
            {
                return Fail("Track graph is not initialized.", out error);
            }

            if (!IsConfirmedPosition(initialPosition))
            {
                return Fail("Initial switch position must be Normal or Reverse.", out error);
            }

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
                {
                    snapshot.edgePairs.Add(new TrackEdgePair
                    {
                        pairId = pair.pairId,
                        edgeAId = pair.edgeAId,
                        edgeBId = pair.edgeBId,
                        condition = pair.condition
                    });
                }

                context.ConnectionsByNodeId.Add(snapshot.nodeId, snapshot);
                if (snapshot.edgePairs.Count == 2)
                {
                    context.StatesById.Add(snapshot.connectionId,
                        new TrackConnectionState(initialPosition, initialPosition, false));
                }
            }

            context.IsInitialized = true;
            error = null;
            return true;
        }

        // 連動装置等からの要求を受け付ける。受付だけでは位置を確定しない。
        public static bool TryRequestPosition(TrackConnectionContext context, string connectionId,
            TrackSwitchPosition position, out string error) =>
            TryRequestPosition(context, connectionId, position, null, out error);

        // nullは手動確定待ち、0は即時確定、正の値はシミュレーション時間で転換する。
        public static bool TryRequestPosition(TrackConnectionContext context, string connectionId,
            TrackSwitchPosition position, float? switchDurationSeconds, out string error)
        {
            if (switchDurationSeconds.HasValue &&
                (switchDurationSeconds.Value < 0f || float.IsNaN(switchDurationSeconds.Value) ||
                 float.IsInfinity(switchDurationSeconds.Value)))
            {
                return Fail("Switch duration must be a non-negative finite value.", out error);
            }

            if (!IsConfirmedPosition(position))
            {
                return Fail("Requested position must be Normal or Reverse.", out error);
            }

            if (!context.TryGetState(connectionId, out var state))
            {
                return Fail($"Switch '{connectionId}' is unavailable or not initialized.", out error);
            }

            if (state.IsMoving && state.RequestedPosition != position)
            {
                return Fail($"Switch '{connectionId}' is already moving to another position.", out error);
            }

            if (!state.IsMoving && state.ActualPosition != position)
            {
                context.StatesById[connectionId] =
                    new TrackConnectionState(position, TrackSwitchPosition.Unknown, true);
                context.SwitchRemainingSecondsById.Remove(connectionId);
                if (switchDurationSeconds == 0f)
                {
                    return TryConfirmPosition(context, connectionId, position, out error);
                }
                if (switchDurationSeconds.HasValue)
                {
                    context.SwitchRemainingSecondsById[connectionId] = switchDurationSeconds.Value;
                }
            }

            error = null;
            return true;
        }

        // 転換動作の完了通知を受け、要求と一致した位置だけを確定する。
        public static bool TryConfirmPosition(TrackConnectionContext context, string connectionId,
            TrackSwitchPosition position, out string error)
        {
            if (!IsConfirmedPosition(position))
            {
                return Fail("Confirmed position must be Normal or Reverse.", out error);
            }

            if (!context.TryGetState(connectionId, out var state))
            {
                return Fail($"Switch '{connectionId}' is unavailable or not initialized.", out error);
            }

            if (state.RequestedPosition != position)
            {
                return Fail($"Confirmation for '{connectionId}' does not match the requested position.", out error);
            }

            context.StatesById[connectionId] = new TrackConnectionState(position, position, false);
            context.SwitchRemainingSecondsById.Remove(connectionId);
            error = null;
            return true;
        }

        public static void Calculate(TrackConnectionContext context, float deltaTimeSeconds)
        {
            if (!context.IsInitialized)
            {
                context.SwitchRemainingSecondsById.Clear();
                context.SwitchTimerKeys.Clear();
                return;
            }
            if (deltaTimeSeconds <= 0f || float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds))
            {
                return;
            }

            // 完了通知で辞書から削除するため、キーを再利用するリストへコピーする。
            context.SwitchTimerKeys.Clear();
            context.SwitchTimerKeys.AddRange(context.SwitchRemainingSecondsById.Keys);
            foreach (string connectionId in context.SwitchTimerKeys)
            {
                if (!context.TryGetState(connectionId, out var state) || !state.IsMoving)
                {
                    context.SwitchRemainingSecondsById.Remove(connectionId);
                    continue;
                }

                double remaining = context.SwitchRemainingSecondsById[connectionId] - deltaTimeSeconds;
                if (remaining <= 0d)
                {
                    TryConfirmPosition(context, connectionId, state.RequestedPosition, out _);
                }
                else
                {
                    context.SwitchRemainingSecondsById[connectionId] = remaining;
                }
            }
            context.SwitchTimerKeys.Clear();
        }

        // 確定した転轍機位置から、接続ペアの相手Edgeを返す。
        public static bool TryResolveNextEdge(TrackConnectionContext context, string nodeId,
            string incomingEdgeId, out string nextEdgeId, out string error)
        {
            nextEdgeId = null;
            if (!context.IsInitialized)
            {
                return Fail("Track connections are not initialized.", out error);
            }

            if (string.IsNullOrWhiteSpace(nodeId) ||
                !context.ConnectionsByNodeId.TryGetValue(nodeId, out var connection))
            {
                return Fail($"Node '{nodeId}' has no connection definition.", out error);
            }

            var condition = TrackConnectionCondition.Always;
            if (connection.edgePairs.Count == 2)
            {
                if (!context.TryGetState(connection.connectionId, out var state) || state.IsMoving ||
                    !IsConfirmedPosition(state.ActualPosition))
                {
                    return Fail($"Switch '{connection.connectionId}' has no confirmed position.", out error);
                }

                condition = state.ActualPosition == TrackSwitchPosition.Normal
                    ? TrackConnectionCondition.Normal : TrackConnectionCondition.Reverse;
            }

            foreach (var pair in connection.edgePairs)
            {
                if (pair.condition != condition)
                {
                    continue;
                }

                if (pair.edgeAId == incomingEdgeId)
                {
                    nextEdgeId = pair.edgeBId;
                }
                else if (pair.edgeBId == incomingEdgeId)
                {
                    nextEdgeId = pair.edgeAId;
                }

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
