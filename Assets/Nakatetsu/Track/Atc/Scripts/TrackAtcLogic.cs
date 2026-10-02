using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    public static class TrackAtcLogic
    {
        public const int guard = 512;
        public static void Calculate(TrackAtcContext context, float deltaTimeSeconds)
        {
            if (context == null)
            {
                return;
            }

            context.Output.telegrams.Clear();
            if (!TryInitialize(context))
            {
                return;
            }

            TryUpdateNextEdges(context, deltaTimeSeconds);
        }

        private static bool TryInitialize(TrackAtcContext context)
        {
            var workspace = context.Workspace;
            workspace.atcEdgesById.Clear();
            workspace.atcNodesById.Clear();
            workspace.atcRoutesById.Clear();
            workspace.visitedAtcEdgeIds.Clear();

            var graph = context.Graph;
            if (graph?.atcEdge == null || graph.atcNode == null || graph.routes == null)
            {
                return false;
            }

            // 定義を更新した場合も、前回のIDや接続を残さない。
            foreach (var node in graph.atcNode)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.atcNodeId) ||
                    node.connectedAtcEdgeIds == null || !workspace.atcNodesById.TryAdd(node.atcNodeId, node))
                {
                    return false;
                }
            }

            foreach (var edge in graph.atcEdge)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.atcEdgeId) ||
                    string.IsNullOrWhiteSpace(edge.trackCircuitId) ||
                    string.IsNullOrWhiteSpace(edge.atcNodeAId) || string.IsNullOrWhiteSpace(edge.atcNodeBId) ||
                    edge.atcNodeAId == edge.atcNodeBId ||
                    !workspace.atcNodesById.ContainsKey(edge.atcNodeAId) ||
                    !workspace.atcNodesById.ContainsKey(edge.atcNodeBId) ||
                    !workspace.atcEdgesById.TryAdd(edge.atcEdgeId, edge))
                {
                    return false;
                }
            }

            foreach (var node in graph.atcNode)
            {
                var connectedIds = new HashSet<string>();
                foreach (string edgeId in node.connectedAtcEdgeIds)
                {
                    if (string.IsNullOrWhiteSpace(edgeId) || !connectedIds.Add(edgeId) ||
                        !workspace.atcEdgesById.TryGetValue(edgeId, out var edge) ||
                        (edge.atcNodeAId != node.atcNodeId && edge.atcNodeBId != node.atcNodeId))
                    {
                        return false;
                    }
                }
            }

            foreach (var edge in graph.atcEdge)
            {
                if (!workspace.atcNodesById[edge.atcNodeAId].connectedAtcEdgeIds.Contains(edge.atcEdgeId) ||
                    !workspace.atcNodesById[edge.atcNodeBId].connectedAtcEdgeIds.Contains(edge.atcEdgeId))
                {
                    return false;
                }
            }

            foreach (var route in graph.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.atcRouteId) ||
                    string.IsNullOrWhiteSpace(route.interlockingRouteId) || route.atcEdgeIds == null ||
                    route.atcEdgeIds.Count < 2 || !workspace.atcRoutesById.TryAdd(route.atcRouteId, route))
                {
                    return false;
                }

                var routeEdgeIds = new HashSet<string>();
                foreach (string edgeId in route.atcEdgeIds)
                {
                    if (string.IsNullOrWhiteSpace(edgeId) || !routeEdgeIds.Add(edgeId) ||
                        !workspace.atcEdgesById.ContainsKey(edgeId))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool TryUpdateNextEdges(
            TrackAtcContext context,
            float deltaTimeSeconds
        )
        {
            if (context.Graph == null || context.Graph.atcEdge == null)
            {
                return false;
            }

            context.Workspace.visitedAtcEdgeIds.Clear();

            // 閉塞と成立中の進路から先に辿り、末尾Edgeだけを単独で再探索しない。
            foreach (var edge in context.Graph.atcEdge)
            {
                if (edge.controlKind == TrackAtcEdgeControlKind.Block &&
                    !context.Workspace.visitedAtcEdgeIds.Contains(edge.atcEdgeId))
                {
                    CalculateNextEdge(context, edge);
                }
            }

            foreach (var route in context.Workspace.atcRoutesById.Values)
            {
                if (!context.Input.RoutesById.TryGetValue(route.interlockingRouteId, out var state) ||
                    !state.PathEstablished || state.CancelPending)
                {
                    continue;
                }

                for (int i = 0; i < route.atcEdgeIds.Count - 1; i++)
                {
                    if (i == 0)
                    {
                        if (!state.ProceedAllowed)
                        {
                            continue;
                        }
                    }
                    else if (!state.RouteLocked)
                    {
                        continue;
                    }

                    string edgeId = route.atcEdgeIds[i];
                    if (!context.Workspace.visitedAtcEdgeIds.Contains(edgeId))
                    {
                        CalculateNextEdge(context, context.Workspace.atcEdgesById[edgeId]);
                    }
                }
            }

            foreach (var atcEdge in context.Graph.atcEdge)
            {
                if (atcEdge.atcEdgeId == null)
                {
                    return false;
                }

                if (context.Workspace.visitedAtcEdgeIds.Contains(atcEdge.atcEdgeId))
                {
                    continue;
                }

                CalculateNextEdge(
                    context,
                    atcEdge
                );
            }

            return true;
        }

        public static void CalculateNextEdge(
            TrackAtcContext context,
            TrackAtcGraphEdge startAtcEdge
        )
        {
            TrackAtcGraphEdge currentEdge = startAtcEdge;
            TrackAtcRouteDefinition currentRoute = null;
            int currentRouteIndex = -1;
            OverrunProtectionMode currentOverrunProtectionMode = OverrunProtectionMode.None;
            bool calculationFailed = false;

            List<string> atcEdgePath = new () {startAtcEdge.atcEdgeId};

            int i;
            for (i = 0; i < guard; i++)
            {

                // 制御未設定のエッジであればそこを停止限界にする
                if (currentEdge.controlKind == TrackAtcEdgeControlKind.Unspecified)
                {
                    break;
                }

                else if (currentEdge.controlKind == TrackAtcEdgeControlKind.Block)
                {
                    if (!TryResolveNextEdgeOnBlock(context, currentEdge, out var nextAtcEdge, out var nextRoute))
                    {
                        calculationFailed = true;
                        break;
                    }

                    // 正常終了したが、今のエッジが停止限界のとき
                    if (nextAtcEdge == null)
                    {
                        break;
                    }

                    atcEdgePath.Add(nextAtcEdge.atcEdgeId);

                    currentEdge = nextAtcEdge;
                    currentRoute = nextRoute;
                    currentRouteIndex = nextRoute != null ? 0 : -1;
                }
                else if (currentEdge.controlKind == TrackAtcEdgeControlKind.Interlocking)
                {
                    if (!TryResolveNextEdgeOnInterlocking(context, currentEdge, currentRoute, currentRouteIndex,
                            out var nextAtcEdge, out var nextRoute, out int nextRouteIndex, out var overrunProtectionMode))
                    {
                        calculationFailed = true;
                        break;
                    }

                    currentOverrunProtectionMode = overrunProtectionMode;
                    if (nextAtcEdge == null)
                    {
                        break;
                    }

                    atcEdgePath.Add(nextAtcEdge.atcEdgeId);
                    currentEdge = nextAtcEdge;
                    currentRoute = nextRoute;
                    currentRouteIndex = nextRouteIndex;
                }

                else
                {
                    // TODO: 構内運転は後で実装
                    calculationFailed = true;
                    break;
                }


            }

            // 停止限界が決まらず探索上限に達した場合は異常とする。
            if (i >= guard)
            {
                calculationFailed = true;
            }


            CreateTrackCircuitAtcTelegram(
                context,
                atcEdgePath,
                currentOverrunProtectionMode,
                calculationFailed
            );
        }

        public static void CreateTrackCircuitAtcTelegram(
            TrackAtcContext context,
            List<string> atcEdgePath,
            OverrunProtectionMode overrunProtectionMode,
            bool calculationFailed
        )
        {
            if (context == null || atcEdgePath == null || atcEdgePath.Count == 0)
            {
                return;
            }

            var edges = new List<TrackAtcGraphEdge>();
            var edgeIds = new HashSet<string>();
            foreach (string edgeId in atcEdgePath)
            {
                if (string.IsNullOrWhiteSpace(edgeId) ||
                    !context.Workspace.atcEdgesById.TryGetValue(edgeId, out var edge) || edge == null)
                {
                    calculationFailed = true;
                    continue;
                }

                edges.Add(edge);
                if (!edgeIds.Add(edgeId) || string.IsNullOrWhiteSpace(edge.trackCircuitId) ||
                    string.IsNullOrWhiteSpace(edge.atcNodeAId) || string.IsNullOrWhiteSpace(edge.atcNodeBId) ||
                    edge.atcNodeAId == edge.atcNodeBId)
                {
                    calculationFailed = true;
                }
            }

            if (double.IsNaN(context.Input.simulationTimeSeconds) ||
                double.IsInfinity(context.Input.simulationTimeSeconds) || context.Input.simulationTimeSeconds < 0d ||
                (overrunProtectionMode != OverrunProtectionMode.None &&
                 overrunProtectionMode != OverrunProtectionMode.Normal &&
                 overrunProtectionMode != OverrunProtectionMode.Restricted))
            {
                calculationFailed = true;
            }

            var directions = new TrackAtcTravelDirection[edges.Count];
            if (!calculationFailed && !TryGetAtcEdgePathDirections(edges, directions))
            {
                calculationFailed = true;
            }

            // 辿った各Edgeの所属回路へ、同じ停止限界に向かう電文をまとめる。
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (string.IsNullOrWhiteSpace(edge.trackCircuitId))
                {
                    continue;
                }

                if (!context.Output.telegrams.TryGetValue(edge.trackCircuitId, out var telegram))
                {
                    telegram = new TrackCircuitAtcTelegram
                    {
                        issuedAtSeconds = context.Input.simulationTimeSeconds,
                        isValid = true
                    };
                    context.Output.telegrams.Add(edge.trackCircuitId, telegram);
                }

                // 同じ更新内で一度でも計算に失敗した回路は、成功した経路で有効に戻さない。
                if (calculationFailed)
                {
                    telegram.isValid = false;
                    continue;
                }

                var lastEdge = edges[edges.Count - 1];
                context.Workspace.visitedAtcEdgeIds.Add(edge.atcEdgeId);

                telegram.atcRouteInfomation[(edge.atcEdgeId, directions[i])] = new TrackCircuitAtcRouteInfomation
                {
                    // 元の探索リストや他の電文と、書き換え可能なリストを共有しない。
                    atcEdgePath = atcEdgePath.GetRange(i, atcEdgePath.Count - i),
                    stopAtcEdgeId = lastEdge.atcEdgeId,
                    overrunProtectionMode = overrunProtectionMode
                };
            }
        }

        private static bool TryGetAtcEdgePathDirections(
            List<TrackAtcGraphEdge> edges,
            TrackAtcTravelDirection[] directions)
        {
            if (edges.Count == 0)
            {
                return false;
            }

            // 1本だけなら接続順から方向を求められないため、Edgeの方向を使う。
            if (edges.Count == 1)
            {
                directions[0] = edges[0].direction;
            }

            for (int i = 0; i < edges.Count - 1; i++)
            {
                var current = edges[i];
                var next = edges[i + 1];
                bool sharedA = current.atcNodeAId == next.atcNodeAId || current.atcNodeAId == next.atcNodeBId;
                bool sharedB = current.atcNodeBId == next.atcNodeAId || current.atcNodeBId == next.atcNodeBId;
                if (sharedA == sharedB)
                {
                    return false;
                }

                string exitNodeId = current.atcNodeBId;
                var direction = TrackAtcTravelDirection.AtoB;
                if (sharedA)
                {
                    exitNodeId = current.atcNodeAId;
                    direction = TrackAtcTravelDirection.BtoA;
                }

                // 直前のEdgeから進入した方向と、次のEdgeへの退出方向を一致させる。
                if (i > 0 && directions[i] != direction)
                {
                    return false;
                }

                directions[i] = direction;
                if (next.atcNodeAId == exitNodeId)
                {
                    directions[i + 1] = TrackAtcTravelDirection.AtoB;
                }
                else
                {
                    directions[i + 1] = TrackAtcTravelDirection.BtoA;
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (directions[i] != TrackAtcTravelDirection.AtoB && directions[i] != TrackAtcTravelDirection.BtoA)
                {
                    return false;
                }

                if (edges[i].controlKind == TrackAtcEdgeControlKind.Block && edges[i].direction != directions[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryResolveNextEdgeOnBlock(
            TrackAtcContext context,
            TrackAtcGraphEdge currentEdge,
            out TrackAtcGraphEdge nextAtcEdge,
            out TrackAtcRouteDefinition nextRoute
        )
        {
            nextAtcEdge = null;
            nextRoute = null;

            if (currentEdge.direction == TrackAtcTravelDirection.Unspecified)
                return false;

            string exitAtcNodeId = currentEdge.direction == TrackAtcTravelDirection.AtoB
                ? currentEdge.atcNodeBId
                : currentEdge.atcNodeAId;

            if (!context.Workspace.atcNodesById.TryGetValue(exitAtcNodeId, out var exitAtcNode) ||
                exitAtcNode.connectedAtcEdgeIds == null ||
                !exitAtcNode.connectedAtcEdgeIds.Contains(currentEdge.atcEdgeId))
                return false;

            int count = exitAtcNode.connectedAtcEdgeIds.Count;

            // 正常な行き止まり。
            if (count == 1)
            {
                return true;
            }
            if (count != 2 && count != 3)
            {
                return false;
            }

            // 2本なら残りの1本、3本なら有効な連動進路の先頭を選ぶ。
            foreach (string edgeId in exitAtcNode.connectedAtcEdgeIds)
            {
                // 自分自身を選ばないようにする
                if (edgeId == currentEdge.atcEdgeId)
                {
                    continue;
                }

                if (!context.Workspace.atcEdgesById.TryGetValue(edgeId, out var candidate))
                {
                    nextAtcEdge = null;
                    nextRoute = null;
                    return false;
                }


                if (count == 3 && candidate.controlKind != TrackAtcEdgeControlKind.Interlocking)
                {
                    continue;
                }

                if (!CanEnterFromBlock(context, candidate, exitAtcNodeId, out var candidateRoute))
                {
                    continue;
                }

                // 複数の枝が有効なら、列挙順で選ばない。
                if (nextAtcEdge != null)
                {
                    nextAtcEdge = null;
                    nextRoute = null;
                    return false;
                }

                nextAtcEdge = candidate;
                nextRoute = candidateRoute;
            }

            // 経路を決めてから占有を照査する。同じ回路の占有は無視する。
            if (
                nextAtcEdge != null &&
                nextAtcEdge.trackCircuitId != currentEdge.trackCircuitId && (
                   !context.Input.OccupiedByCircuitId.TryGetValue(nextAtcEdge.trackCircuitId, out bool occupied) ||
                    occupied
                )
            )
            {
                nextAtcEdge = null;
                nextRoute = null;
            }


            return true;
        }

        public static bool TryResolveNextEdgeOnInterlocking(
            TrackAtcContext context,
            TrackAtcGraphEdge currentEdge,
            TrackAtcRouteDefinition currentRoute,
            int currentRouteIndex,
            out TrackAtcGraphEdge nextAtcEdge,
            out TrackAtcRouteDefinition nextRoute,
            out int nextRouteIndex,
            out OverrunProtectionMode overrunProtection
        )
        {
            nextAtcEdge = null;
            nextRoute = null;
            nextRouteIndex = -1;
            overrunProtection = OverrunProtectionMode.None;

            TrackAtcRouteDefinition selectedRoute = null;
            int selectedIndex = -1;
            string exitNodeId = null;


            if (currentRoute != null)
            {
                if (!TryGetRouteExitNode(context, currentEdge, currentRoute, currentRouteIndex, out exitNodeId))
                {
                    return false;
                }
            }

            if (currentRoute == null || currentRouteIndex == currentRoute.atcEdgeIds.Count - 1)
            {
                foreach (var route in context.Workspace.atcRoutesById.Values)
                {
                    if (route?.atcEdgeIds == null || string.IsNullOrWhiteSpace(route.interlockingRouteId))
                    {
                        return false;
                    }

                    int index = route.atcEdgeIds.IndexOf(currentEdge.atcEdgeId);
                    if (index < 0 || index == route.atcEdgeIds.Count - 1)
                    {
                        continue;
                    }

                    // 進路末尾からは、現在Edgeを先頭として共有する後続進路だけを探す。
                    if (currentRoute != null && index != 0)
                    {
                        continue;
                    }


                    // その進路に進行許可があるか確認する
                    if (!context.Input.RoutesById.TryGetValue(route.interlockingRouteId, out var state) ||
                        !state.PathEstablished || state.CancelPending)
                    {
                        continue;
                    }

                    // 先頭からの新規進入と、進入済み進路の途中からの探索を分ける。
                    if (index == 0)
                    {
                        if (!state.ProceedAllowed)
                        {
                            continue;
                        }
                    }
                    else if (!state.RouteLocked)
                    {
                        continue;
                    }

                    if (!TryGetRouteExitNode(context, currentEdge, route, index, out string candidateExitNodeId))
                    {
                        return false;
                    }

                    // 後続進路へ切り替えても、共有Edge上で進行方向を反転させない。
                    if (exitNodeId != null && candidateExitNodeId != exitNodeId)
                    {
                        continue;
                    }

                    if (selectedRoute != null)
                    {
                        return false;
                    }

                    selectedRoute = route;
                    selectedIndex = index;
                }

                if (selectedRoute == null)
                {
                    if (!TryResolveExitBlock(context, currentEdge, exitNodeId, out nextAtcEdge))
                    {
                        return false;
                    }

                    if (nextAtcEdge != null)
                    {
                        // Blockへの退出後は進路を引き継がない。
                        nextRoute = null;
                        nextRouteIndex = -1;
                        return true;
                    }

                    if (currentRoute == null)
                    {
                        // 終端進路を特定できない。
                        return false;
                    }

                    if (!context.Input.RoutesById.TryGetValue(currentRoute.interlockingRouteId, out var currentRouteState))
                    {
                        return false;
                    }

                    overrunProtection = currentRouteState.OverrunMode;

                    return true;
                }
            }
            else
            {
                if (!context.Input.RoutesById.TryGetValue(currentRoute.interlockingRouteId, out var state) ||
                    !state.PathEstablished || !state.RouteLocked || state.CancelPending)
                {
                    return true;
                }

                selectedRoute = currentRoute;
                selectedIndex = currentRouteIndex;
            }

            if (!TryGetRouteExitNode(context, currentEdge, selectedRoute, selectedIndex, out exitNodeId) ||
                !context.Workspace.atcEdgesById.TryGetValue(selectedRoute.atcEdgeIds[selectedIndex + 1], out var candidate) ||
                candidate == null ||
                (candidate.atcNodeAId != exitNodeId && candidate.atcNodeBId != exitNodeId))
            {
                return false;
            }

            // 経路を決めてから占有を照査する。同じ回路の占有は無視する。
            if (candidate.trackCircuitId != currentEdge.trackCircuitId &&
                (!context.Input.OccupiedByCircuitId.TryGetValue(candidate.trackCircuitId, out bool occupied) || occupied))
            {
                return true;
            }

            nextAtcEdge = candidate;
            nextRoute = selectedRoute;
            nextRouteIndex = selectedIndex + 1;
            return true;
        }

        private static bool TryResolveExitBlock(
            TrackAtcContext context,
            TrackAtcGraphEdge currentEdge,
            string exitNodeId,
            out TrackAtcGraphEdge nextAtcEdge)
        {
            nextAtcEdge = null;

            // 退出側が分からなければ現在Edgeの終端まで。
            if (exitNodeId == null)
            {
                return true;
            }

            if (!context.Workspace.atcNodesById.TryGetValue(exitNodeId, out var exitNode) ||
                exitNode == null || exitNode.connectedAtcEdgeIds == null ||
                !exitNode.connectedAtcEdgeIds.Contains(currentEdge.atcEdgeId))
            {
                return false;
            }

            // 後続進路がなければ、退出側から進入できる向きのBlockを探す。
            TrackAtcGraphEdge nextBlock = null;
            foreach (string edgeId in exitNode.connectedAtcEdgeIds)
            {
                if (edgeId == currentEdge.atcEdgeId)
                {
                    continue;
                }

                if (!context.Workspace.atcEdgesById.TryGetValue(edgeId, out var block) || block == null)
                {
                    return false;
                }

                if (block.controlKind != TrackAtcEdgeControlKind.Block)
                {
                    continue;
                }

                bool canEnter = block.direction == TrackAtcTravelDirection.AtoB && block.atcNodeAId == exitNodeId ||
                    block.direction == TrackAtcTravelDirection.BtoA && block.atcNodeBId == exitNodeId;
                if (!canEnter)
                {
                    continue;
                }

                // 複数のBlockが有効なら、列挙順で選ばない。
                if (nextBlock != null)
                {
                    return false;
                }

                nextBlock = block;
            }

            // 経路を決めてから占有を照査する。同じ回路の占有は無視する。
            if (nextBlock != null && nextBlock.trackCircuitId != currentEdge.trackCircuitId &&
                (!context.Input.OccupiedByCircuitId.TryGetValue(nextBlock.trackCircuitId, out bool blockOccupied) || blockOccupied))
            {
                return true;
            }

            nextAtcEdge = nextBlock;
            return true;
        }

        private static bool TryGetRouteExitNode(
            TrackAtcContext context,
            TrackAtcGraphEdge currentEdge,
            TrackAtcRouteDefinition route,
            int routeIndex,
            out string exitNodeId)
        {
            exitNodeId = null;
            if (route?.atcEdgeIds == null || route.atcEdgeIds.Count < 2 ||
                routeIndex < 0 || routeIndex >= route.atcEdgeIds.Count ||
                route.atcEdgeIds[routeIndex] != currentEdge.atcEdgeId)
            {
                return false;
            }

            // 途中・末尾では直前のEdge、先頭では次のEdgeとの接続から退出側を求める。
            int adjacentIndex = routeIndex > 0 ? routeIndex - 1 : 1;
            if (!context.Workspace.atcEdgesById.TryGetValue(route.atcEdgeIds[adjacentIndex], out var adjacent) ||
                adjacent == null)
            {
                return false;
            }

            bool sharedA = currentEdge.atcNodeAId == adjacent.atcNodeAId || currentEdge.atcNodeAId == adjacent.atcNodeBId;
            bool sharedB = currentEdge.atcNodeBId == adjacent.atcNodeAId || currentEdge.atcNodeBId == adjacent.atcNodeBId;
            if (sharedA == sharedB)
            {
                return false;
            }

            exitNodeId = routeIndex > 0
                ? (sharedA ? currentEdge.atcNodeBId : currentEdge.atcNodeAId)
                : (sharedA ? currentEdge.atcNodeAId : currentEdge.atcNodeBId);
            return true;
        }

        private static bool CanEnterFromBlock(
            TrackAtcContext context,
            TrackAtcGraphEdge edge,
            string entryNodeId,
            out TrackAtcRouteDefinition nextRoute)
        {
            nextRoute = null;

            if (edge.controlKind == TrackAtcEdgeControlKind.Block)
            {
                return edge.direction == TrackAtcTravelDirection.AtoB && edge.atcNodeAId == entryNodeId ||
                    edge.direction == TrackAtcTravelDirection.BtoA && edge.atcNodeBId == entryNodeId;
            }
            if (edge.controlKind == TrackAtcEdgeControlKind.Yard)
            {
                // TODO: 後で構内運転を実装
                return false;
            }


            string exitNodeId;

            if (edge.atcNodeAId == entryNodeId)
            {
                exitNodeId = edge.atcNodeBId;
            }
            else if (edge.atcNodeBId == entryNodeId)
            {
                exitNodeId = edge.atcNodeAId;
            }

            else
            {
                return false;
            }


            foreach (var route in context.Workspace.atcRoutesById.Values)
            {
                if (
                    route.atcEdgeIds.Count < 2 ||
                    route.atcEdgeIds[0] != edge.atcEdgeId ||
                    !context.Input.RoutesById.TryGetValue(route.interlockingRouteId, out var state) ||
                    !state.ProceedAllowed || !state.PathEstablished || state.CancelPending
                )
                {
                    continue;
                }


                // 進路の2本目が退出側につながることを確認する。(逆向きの進路を許可しない)
                if (
                    context.Workspace.atcEdgesById.TryGetValue(route.atcEdgeIds[1], out var secondEdge) && (
                        secondEdge.atcNodeAId == exitNodeId
                        || secondEdge.atcNodeBId == exitNodeId
                    )
                )
                {
                    nextRoute = route;
                    return true;
                }

            }

            return false;
        }
    }
}
