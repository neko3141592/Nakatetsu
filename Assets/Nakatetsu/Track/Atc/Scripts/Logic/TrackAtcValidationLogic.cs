using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    public static class TrackAtcValidationLogic
    {
        internal static void UpdateValidation(TrackAtcContext context)
        {
            if (context?.State?.validation == null)
            {
                return;
            }

            var state = context.State.validation;
            ResetState(state);

            state.isGraphValid = TryPrepareGraph(context.Graph, state, out string graphFailureReason);
            if (!state.isGraphValid)
            {
                // 検証途中の辞書も残さず、後段が未確認の定義を使用できないようにする。
                ClearGraphIndices(state);
                state.failureReason = graphFailureReason;
            }

            ValidateSimulationTime(context.Input, state);
            if (state.isGraphValid)
            {
                RecordInputAvailability(context.Input, state);
            }
        }

        private static void ResetState(TrackAtcValidationState state)
        {
            state.isGraphValid = false;
            state.isSimulationTimeValid = false;
            state.failureReason = string.Empty;
            ClearGraphIndices(state);
            state.hasCircuitInputById.Clear();
            state.hasRouteInputById.Clear();
        }

        private static void ClearGraphIndices(TrackAtcValidationState state)
        {
            state.atcEdgesById.Clear();
            state.atcNodesById.Clear();
            state.atcRoutesById.Clear();
            state.directionsByAtcEdgeId.Clear();
        }

        private static bool TryPrepareGraph(
            TrackAtcGraphDefinition graph,
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (graph?.atcEdge == null || graph.atcNode == null || graph.routes == null)
            {
                failureReason = "ATC Graphまたは定義リストを取得できない。";
                return false;
            }

            if (!TryIndexNodes(graph, state, out failureReason)
                || !TryIndexEdges(graph, state, out failureReason)
                || !TryValidateConnections(state, out failureReason)
                || !TryIndexRoutes(graph, state, out failureReason)
                || !TryPrepareDirections(state, out failureReason))
            {
                return false;
            }

            return true;
        }

        private static bool TryIndexNodes(
            TrackAtcGraphDefinition graph,
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (var node in graph.atcNode)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.atcNodeId)
                    || node.connectedAtcEdgeIds == null
                    || !state.atcNodesById.TryAdd(node.atcNodeId, node))
                {
                    failureReason = "ATC NodeのID、接続リスト、またはIDの一意性が不正。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryIndexEdges(
            TrackAtcGraphDefinition graph,
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (var edge in graph.atcEdge)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.atcEdgeId)
                    || string.IsNullOrWhiteSpace(edge.trackCircuitId)
                    || string.IsNullOrWhiteSpace(edge.atcNodeAId)
                    || string.IsNullOrWhiteSpace(edge.atcNodeBId)
                    || edge.atcNodeAId == edge.atcNodeBId
                    || !state.atcNodesById.ContainsKey(edge.atcNodeAId)
                    || !state.atcNodesById.ContainsKey(edge.atcNodeBId)
                    || !state.atcEdgesById.TryAdd(edge.atcEdgeId, edge))
                {
                    failureReason = "ATC EdgeのID、所属回路、端点、またはIDの一意性が不正。";
                    return false;
                }

                if (edge.controlKind != TrackAtcEdgeControlKind.Block
                    && edge.controlKind != TrackAtcEdgeControlKind.Interlocking
                    && edge.controlKind != TrackAtcEdgeControlKind.Yard)
                {
                    failureReason = $"ATC Edge {edge.atcEdgeId} の制御方式が未指定または不正。";
                    return false;
                }

                if (edge.controlKind == TrackAtcEdgeControlKind.Block
                    && !TryConvertDirection(edge.direction, out _))
                {
                    failureReason = $"Block Edge {edge.atcEdgeId} の通行方向が不正。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateConnections(
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (var node in state.atcNodesById.Values)
            {
                var connectedIds = new HashSet<string>();
                foreach (string edgeId in node.connectedAtcEdgeIds)
                {
                    if (string.IsNullOrWhiteSpace(edgeId) || !connectedIds.Add(edgeId)
                        || !state.atcEdgesById.TryGetValue(edgeId, out var edge)
                        || (edge.atcNodeAId != node.atcNodeId && edge.atcNodeBId != node.atcNodeId))
                    {
                        failureReason = $"ATC Node {node.atcNodeId} の接続Edgeが不正。";
                        return false;
                    }
                }
            }

            foreach (var edge in state.atcEdgesById.Values)
            {
                if (!state.atcNodesById[edge.atcNodeAId].connectedAtcEdgeIds.Contains(edge.atcEdgeId)
                    || !state.atcNodesById[edge.atcNodeBId].connectedAtcEdgeIds.Contains(edge.atcEdgeId))
                {
                    failureReason = $"ATC Edge {edge.atcEdgeId} と端点Nodeの接続が相互に一致しない。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryIndexRoutes(
            TrackAtcGraphDefinition graph,
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (var route in graph.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.atcRouteId)
                    || string.IsNullOrWhiteSpace(route.interlockingRouteId)
                    || route.atcEdgeIds == null || route.atcEdgeIds.Count < 2
                    || !state.atcRoutesById.TryAdd(route.atcRouteId, route))
                {
                    failureReason = "ATC進路のID、連動進路ID、Edge列、またはIDの一意性が不正。";
                    return false;
                }

                var routeEdgeIds = new HashSet<string>();
                foreach (string edgeId in route.atcEdgeIds)
                {
                    if (string.IsNullOrWhiteSpace(edgeId) || !routeEdgeIds.Add(edgeId)
                        || !state.atcEdgesById.ContainsKey(edgeId))
                    {
                        failureReason = $"ATC進路 {route.atcRouteId} の参照Edgeが不正または重複している。";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryPrepareDirections(
            TrackAtcValidationState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (var edge in state.atcEdgesById.Values)
            {
                var directions = new HashSet<TrackEdgeTravelDirection>();
                if (edge.controlKind != TrackAtcEdgeControlKind.Interlocking
                    && TryConvertDirection(edge.direction, out var direction))
                {
                    directions.Add(direction);
                }

                state.directionsByAtcEdgeId.Add(edge.atcEdgeId, directions);
            }

            foreach (var route in state.atcRoutesById.Values)
            {
                if (!TryGetRouteDirections(route, state.atcEdgesById, out var routeDirections))
                {
                    failureReason = $"ATC進路 {route.atcRouteId} の接続または進行方向が不整合。";
                    return false;
                }

                for (int i = 0; i < route.atcEdgeIds.Count; i++)
                {
                    var edge = state.atcEdgesById[route.atcEdgeIds[i]];
                    if (edge.controlKind == TrackAtcEdgeControlKind.Interlocking
                        || edge.controlKind == TrackAtcEdgeControlKind.Yard)
                    {
                        // 進路が未設定でも、静的な順序から決まる方向を探索に使用する。
                        state.directionsByAtcEdgeId[edge.atcEdgeId].Add(routeDirections[i]);
                    }
                }
            }

            foreach (var edge in state.atcEdgesById.Values)
            {
                if (edge.controlKind == TrackAtcEdgeControlKind.Interlocking
                    && state.directionsByAtcEdgeId[edge.atcEdgeId].Count == 0)
                {
                    failureReason = $"Interlocking Edge {edge.atcEdgeId} の方向を静的進路から確定できない。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRouteDirections(
            TrackAtcRouteDefinition route,
            Dictionary<string, TrackAtcGraphEdge> edgesById,
            out TrackEdgeTravelDirection[] directions)
        {
            directions = new TrackEdgeTravelDirection[route.atcEdgeIds.Count];
            for (int i = 0; i < route.atcEdgeIds.Count - 1; i++)
            {
                var current = edgesById[route.atcEdgeIds[i]];
                var next = edgesById[route.atcEdgeIds[i + 1]];
                bool sharedA = current.atcNodeAId == next.atcNodeAId
                    || current.atcNodeAId == next.atcNodeBId;
                bool sharedB = current.atcNodeBId == next.atcNodeAId
                    || current.atcNodeBId == next.atcNodeBId;
                if (sharedA == sharedB)
                {
                    return false;
                }

                var currentDirection = sharedA
                    ? TrackEdgeTravelDirection.BtoA
                    : TrackEdgeTravelDirection.AtoB;
                if (i > 0 && directions[i] != currentDirection)
                {
                    // 同じ端点から進入・退出する列は、途中Edgeを逆転するため進路にできない。
                    return false;
                }

                directions[i] = currentDirection;
                string exitNodeId = sharedA ? current.atcNodeAId : current.atcNodeBId;
                directions[i + 1] = next.atcNodeAId == exitNodeId
                    ? TrackEdgeTravelDirection.AtoB
                    : TrackEdgeTravelDirection.BtoA;
            }

            for (int i = 0; i < route.atcEdgeIds.Count; i++)
            {
                var edge = edgesById[route.atcEdgeIds[i]];
                if (edge.controlKind == TrackAtcEdgeControlKind.Block
                    && (!TryConvertDirection(edge.direction, out var configuredDirection)
                        || configuredDirection != directions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertDirection(
            TrackAtcTravelDirection direction,
            out TrackEdgeTravelDirection convertedDirection)
        {
            convertedDirection = TrackEdgeTravelDirection.Unspecified;
            if (direction == TrackAtcTravelDirection.AtoB)
            {
                convertedDirection = TrackEdgeTravelDirection.AtoB;
                return true;
            }

            if (direction == TrackAtcTravelDirection.BtoA)
            {
                convertedDirection = TrackEdgeTravelDirection.BtoA;
                return true;
            }

            return false;
        }

        private static void ValidateSimulationTime(TrackAtcInput input, TrackAtcValidationState state)
        {
            state.isSimulationTimeValid = input != null
                && !double.IsNaN(input.simulationTimeSeconds)
                && !double.IsInfinity(input.simulationTimeSeconds)
                && input.simulationTimeSeconds >= 0d;
            if (!state.isSimulationTimeValid)
            {
                string reason = "シミュレーション時刻を取得できない、または有限の0以上の値ではない。";
                state.failureReason = string.IsNullOrEmpty(state.failureReason)
                    ? reason
                    : state.failureReason + " " + reason;
            }
        }

        private static void RecordInputAvailability(TrackAtcInput input, TrackAtcValidationState state)
        {
            foreach (var edge in state.atcEdgesById.Values)
            {
                state.hasCircuitInputById[edge.trackCircuitId] = input?.OccupiedByCircuitId != null
                    && input.OccupiedByCircuitId.ContainsKey(edge.trackCircuitId);
            }

            foreach (var route in state.atcRoutesById.Values)
            {
                // 登録済みのIsRouteSet=falseは、取得失敗ではなく確認済みの未設定。
                state.hasRouteInputById[route.interlockingRouteId] = input?.RoutesById != null
                    && input.RoutesById.TryGetValue(route.interlockingRouteId, out var routeInput)
                    && routeInput != null;
            }
        }
    }
}
