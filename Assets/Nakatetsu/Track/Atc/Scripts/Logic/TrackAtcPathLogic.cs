using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;
using Unity.Profiling;

namespace Nakatetsu.Track.Atc
{
    internal static class TrackAtcPathLogic
    {
        private const int MaximumExplorationSteps = 512;
        private static readonly ProfilerMarker PathMarker = new("Nakatetsu.ATC.Path");
        private static readonly ProfilerMarker NextLinkMarker = new("Nakatetsu.ATC.NextLink.Calculate");
        private static readonly ProfilerMarker OriginRouteMarker = new("Nakatetsu.ATC.OriginRoute.Select");

        internal static void UpdatePath(TrackAtcContext context)
        {
            using var pathScope = PathMarker.Auto();
            var path = context.State.path;
            path.resultsByKey.Clear();
            path.nextLinksByKey.Clear();
            path.terminalRoutesByKey.Clear();
            path.selectionFailureByCircuitId.Clear();
            if (!context.State.validation.isGraphValid)
            {
                path.selectedEdgeByCircuitId.Clear();
                return;
            }

            SelectCircuitEdges(context);
            ExploreAllOrigins(context);
            ValidateNextEdgeChains(context);
        }

        private static void SelectCircuitEdges(TrackAtcContext context)
        {
            var edgesByCircuit = new Dictionary<string, List<TrackAtcGraphEdge>>();
            foreach (var edge in context.State.validation.atcEdgesById.Values)
            {
                if (!edgesByCircuit.TryGetValue(edge.trackCircuitId, out var edges))
                {
                    edges = new List<TrackAtcGraphEdge>();
                    edgesByCircuit.Add(edge.trackCircuitId, edges);
                }

                edges.Add(edge);
            }

            var path = context.State.path;
            foreach (string previousCircuitId in new List<string>(path.selectedEdgeByCircuitId.Keys))
            {
                if (!edgesByCircuit.ContainsKey(previousCircuitId))
                {
                    path.selectedEdgeByCircuitId.Remove(previousCircuitId);
                }
            }

            foreach (var pair in edgesByCircuit)
            {
                if (TrySelectCircuitEdge(context, pair.Key, pair.Value, out var selected, out var failureReason))
                {
                    path.selectedEdgeByCircuitId[pair.Key] = selected.atcEdgeId;
                }
                else
                {
                    path.selectionFailureByCircuitId.Add(pair.Key, failureReason);
                }
            }
        }

        private static bool TrySelectCircuitEdge(
            TrackAtcContext context,
            string circuitId,
            List<TrackAtcGraphEdge> candidates,
            out TrackAtcGraphEdge selected,
            out string failureReason)
        {
            selected = null;
            failureReason = string.Empty;
            if (candidates.Count == 1)
            {
                selected = candidates[0];
                return true;
            }

            // 開通済み進路のEdgeを採用し、同じ回路の非選択側は探索しない。
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                bool containsCandidate = false;
                foreach (var candidate in candidates)
                {
                    containsCandidate |= route.atcEdgeIds.Contains(candidate.atcEdgeId);
                }

                if (!containsCandidate)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                if (!CanContinue(input))
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    if (!route.atcEdgeIds.Contains(candidate.atcEdgeId))
                    {
                        continue;
                    }

                    if (selected != null && selected.atcEdgeId != candidate.atcEdgeId)
                    {
                        failureReason = $"Multiple established ATC edges belong to circuit '{circuitId}'.";
                        return false;
                    }

                    selected = candidate;
                }
            }

            if (selected != null)
            {
                return true;
            }

            if (context.Input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) && occupied &&
                context.State.path.selectedEdgeByCircuitId.TryGetValue(circuitId, out string previousEdgeId))
            {
                selected = candidates.Find(edge => edge.atcEdgeId == previousEdgeId);
                if (selected != null)
                {
                    return true;
                }
            }

            // 進路未設定で履歴もない場合だけ、既存の確定済み線路接続を使う。
            foreach (var candidate in candidates)
            {
                if (!context.Input.PhysicalPathAvailableByAtcEdgeId.TryGetValue(candidate.atcEdgeId,
                        out bool available) || !available)
                {
                    continue;
                }

                if (selected != null)
                {
                    failureReason = $"Multiple connected ATC edges belong to circuit '{circuitId}'.";
                    return false;
                }

                selected = candidate;
            }

            if (selected == null)
            {
                failureReason = $"No selected ATC edge is available for circuit '{circuitId}'.";
                return false;
            }

            return true;
        }

        private static void ExploreAllOrigins(TrackAtcContext context)
        {
            foreach (var edge in context.State.validation.atcEdgesById.Values)
            {
                if (!IsSelectedEdge(context, edge))
                {
                    continue;
                }

                if (edge.controlKind == TrackAtcEdgeControlKind.Interlocking ||
                    edge.controlKind == TrackAtcEdgeControlKind.Yard)
                {
                    // 静的進路の方向が片方だけでも、未設定時の停止電文は両方向に必要。
                    AddOrigin(context, edge, TrackEdgeTravelDirection.AtoB);
                    AddOrigin(context, edge, TrackEdgeTravelDirection.BtoA);
                    continue;
                }

                foreach (var direction in context.State.validation.directionsByAtcEdgeId[edge.atcEdgeId])
                {
                    AddOrigin(context, edge, direction);
                }
            }
        }

        private static void AddOrigin(
            TrackAtcContext context,
            TrackAtcGraphEdge edge,
            TrackEdgeTravelDirection direction)
        {
            var key = new TrackAtcEdgeKey(edge.atcEdgeId, direction);
            context.State.path.resultsByKey.Add(key, ExploreOrigin(context, key));
        }

        // ATCエッジ・方向を起点に経路を探索
        private static TrackAtcPathResult ExploreOrigin(TrackAtcContext context, TrackAtcEdgeKey origin)
        {
            var result = new TrackAtcPathResult();
            var visited = new HashSet<TrackAtcEdgeKey>();
            var circuits = new HashSet<string>();
            var currentKey = origin;
            TrackAtcRouteDefinition currentRoute = null;
            int routeIndex = -1;
            var validation = context.State.validation;
            var originEdge = validation.atcEdgesById[origin.atcEdgeId];
            circuits.Add(originEdge.trackCircuitId);

            var originEndReason = TrackAtcPathEndReason.RouteNotSet;
            string originFailure = string.Empty;
            if (originEdge.controlKind != TrackAtcEdgeControlKind.Yard &&
                // 見ているエッジが連動進路内の時、そのエッジを起点とする進路が成立しているか探索する
                !TrySelectOriginRoute(context, origin, out currentRoute, out routeIndex,
                    out originEndReason, out originFailure))
            {
                return Fail(result, circuits, originFailure);
            }
            else if (originEdge.controlKind == TrackAtcEdgeControlKind.Interlocking && currentRoute == null)
            {
                return Stop(context, result, currentKey, originEndReason, circuits);
            }

            for (int step = 0; step < MaximumExplorationSteps; step++)
            {
                var edge = validation.atcEdgesById[currentKey.atcEdgeId];
                circuits.Add(edge.trackCircuitId);
                if (!HasCircuitInput(context, edge.trackCircuitId))
                {
                    return Fail(result, circuits, $"Circuit input '{edge.trackCircuitId}' is unavailable.");
                }

                if (!visited.Add(currentKey))
                {
                    return Fail(result, circuits, "ATC path contains a cycle.");
                }

                if (!TryFindNextEdge(context, currentKey, currentRoute, routeIndex,
                    out var nextKey, out var nextRoute, out int nextRouteIndex,
                    out var endReason, out var failureReason))
                {
                    return Fail(result, circuits, failureReason);
                }

                if (!nextKey.HasValue)
                {
                    return Stop(context, result, currentKey, endReason, circuits);
                }

                var nextEdge = validation.atcEdgesById[nextKey.Value.atcEdgeId];
                if (!IsSelectedEdge(context, nextEdge))
                {
                    return Fail(result, circuits, $"Next edge '{nextEdge.atcEdgeId}' is not selected for its circuit.");
                }

                if (!HasCircuitInput(context, nextEdge.trackCircuitId))
                {
                    circuits.Add(nextEdge.trackCircuitId);
                    return Fail(result, circuits, $"Circuit input '{nextEdge.trackCircuitId}' is unavailable.");
                }

                // 自回路と同一回路内の移動は、自列車の占有によって停止させない。
                if (edge.trackCircuitId != nextEdge.trackCircuitId &&
                    context.Input.OccupiedByCircuitId[nextEdge.trackCircuitId])
                {
                    return Stop(context, result, currentKey,
                        TrackAtcPathEndReason.NextCircuitOccupied, circuits);
                }

                if (currentKey == origin)
                {
                    result.nextEdgeKey = nextKey;
                }

                currentKey = nextKey.Value;
                currentRoute = nextRoute;
                routeIndex = nextRouteIndex;
            }

            return Fail(result, circuits, "ATC path exploration exceeded its step limit.");
        }

        private static bool TryFindNextEdge(
            TrackAtcContext context,
            TrackAtcEdgeKey currentKey,
            TrackAtcRouteDefinition currentRoute,
            int routeIndex,
            out TrackAtcEdgeKey? nextKey,
            out TrackAtcRouteDefinition nextRoute,
            out int nextRouteIndex,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            nextKey = null;
            nextRoute = null;
            nextRouteIndex = -1;
            endReason = TrackAtcPathEndReason.NoNextEdge;
            failureReason = string.Empty;
            var edge = context.State.validation.atcEdgesById[currentKey.atcEdgeId];
            if (edge.controlKind == TrackAtcEdgeControlKind.Yard)
            {
                failureReason = "Yard ATC path calculation is not implemented.";
                return false;
            }

            if (edge.controlKind == TrackAtcEdgeControlKind.Block)
            {
                if (currentRoute != null && routeIndex < currentRoute.atcEdgeIds.Count - 1)
                {
                    // Blockから連動区間へ入る際は、既存の進路内継続条件ではなく新規進入条件を使う。
                    var routeInput = context.Input.RoutesById[currentRoute.InterlockingKey];
                    if (!CanEnter(routeInput))
                    {
                        endReason = GetUnavailableReason(routeInput);
                        return true;
                    }

                    nextRoute = currentRoute;
                    nextRouteIndex = routeIndex + 1;
                    nextKey = GetRouteKey(context, currentRoute, nextRouteIndex);
                    return true;
                }

                return TryFindConnectedEdge(context, currentKey, out nextKey,
                    out nextRoute, out nextRouteIndex, out endReason, out failureReason);
            }

            if (currentRoute == null)
            {
                endReason = TrackAtcPathEndReason.RouteNotSet;
                return true;
            }

            var input = context.Input.RoutesById[currentRoute.InterlockingKey];
            if (!CanContinue(input))
            {
                endReason = GetUnavailableReason(input);
                return true;
            }

            if (routeIndex < currentRoute.atcEdgeIds.Count - 1)
            {
                nextRoute = currentRoute;
                nextRouteIndex = routeIndex + 1;
                nextKey = GetRouteKey(context, currentRoute, nextRouteIndex);
                return true;
            }

            // 前進路の終端と後続進路の起点が同じEdgeでも、新規進入条件を使う。
            if (!TrySelectEntryRoute(context, currentKey, edge.trackCircuitId, currentRoute.atcRouteId,
                out var successor, out endReason, out failureReason))
            {
                return false;
            }

            if (successor != null)
            {
                // 共有Edgeは後続進路の先頭でもある。そこから2本目へ進む際は、
                // Blockから先頭へ入った後と同じ進路内継続条件を適用する。
                var successorInput = context.Input.RoutesById[successor.InterlockingKey];
                if (!CanContinue(successorInput))
                {
                    endReason = GetUnavailableReason(successorInput);
                    return true;
                }

                nextRoute = successor;
                if (successor.atcEdgeIds.Count == 1)
                {
                    return TryFindConnectedEdge(context, currentKey, out nextKey,
                        out nextRoute, out nextRouteIndex, out endReason, out failureReason);
                }

                nextRouteIndex = 1;
                nextKey = GetRouteKey(context, successor, nextRouteIndex);
                return true;
            }

            // 信号機を回路境界に置くと、後続進路は隣接Edgeから始まる。
            // 共有Edgeの場合と同じく、確認済みの新規進入許可を照査する。
            if (!TryFindConnectedEdge(context, currentKey, out nextKey,
                out nextRoute, out nextRouteIndex, out _, out failureReason))
            {
                return false;
            }

            if (!nextKey.HasValue)
            {
                endReason = TrackAtcPathEndReason.RouteEnd;
            }

            return true;
        }

        private static bool TryFindConnectedEdge(
            TrackAtcContext context,
            TrackAtcEdgeKey currentKey,
            out TrackAtcEdgeKey? nextKey,
            out TrackAtcRouteDefinition nextRoute,
            out int nextRouteIndex,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            var links = context.State.path.nextLinksByKey;
            if (!links.TryGetValue(currentKey, out var link) || !link.isCalculated)
            {
                link.isValid = CalculateNextLink(context, currentKey, out link.nextEdgeKey,
                    out link.nextRoute, out link.nextRouteIndex, out link.endReason, out link.failureReason);
                // 失敗も確定した結果として共有する。循環検出のvisitedとは分ける。
                link.isCalculated = true;
                links[currentKey] = link;
            }

            nextKey = link.nextEdgeKey;
            nextRoute = link.nextRoute;
            nextRouteIndex = link.nextRouteIndex;
            endReason = link.endReason;
            failureReason = link.failureReason;
            return link.isValid;
        }

        private static bool CalculateNextLink(
            TrackAtcContext context,
            TrackAtcEdgeKey currentKey,
            out TrackAtcEdgeKey? nextKey,
            out TrackAtcRouteDefinition nextRoute,
            out int nextRouteIndex,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            using var nextLinkScope = NextLinkMarker.Auto();
            nextKey = null;
            nextRoute = null;
            nextRouteIndex = -1;
            endReason = TrackAtcPathEndReason.NoNextEdge;
            failureReason = string.Empty;
            var validation = context.State.validation;
            var edge = validation.atcEdgesById[currentKey.atcEdgeId];
            string exitNode = currentKey.direction == TrackEdgeTravelDirection.AtoB
                ? edge.atcNodeBId : edge.atcNodeAId;
            foreach (string candidateId in validation.atcNodesById[exitNode].connectedAtcEdgeIds)
            {
                if (candidateId == currentKey.atcEdgeId)
                {
                    continue;
                }

                var candidate = validation.atcEdgesById[candidateId];
                if (candidate.trackCircuitId == edge.trackCircuitId)
                {
                    // 定位・反位は同じ回路の代替Edgeであり、相互の継続先ではない。
                    continue;
                }

                var direction = candidate.atcNodeAId == exitNode
                    ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA;
                var key = new TrackAtcEdgeKey(candidateId, direction);
                TrackAtcRouteDefinition route = null;
                if (candidate.controlKind == TrackAtcEdgeControlKind.Block)
                {
                    if (!IsSelectedEdge(context, candidate))
                    {
                        continue;
                    }

                    if (!validation.directionsByAtcEdgeId[candidateId].Contains(direction))
                    {
                        continue;
                    }
                }
                else if (candidate.controlKind == TrackAtcEdgeControlKind.Interlocking)
                {
                    if (!TrySelectEntryRoute(context, key, edge.trackCircuitId, null, out route,
                        out var candidateEndReason, out failureReason))
                    {
                        return false;
                    }

                    if (route == null)
                    {
                        if (endReason == TrackAtcPathEndReason.NoNextEdge ||
                            candidateEndReason != TrackAtcPathEndReason.RouteNotSet)
                        {
                            endReason = candidateEndReason;
                        }

                        continue;
                    }

                    if (!IsSelectedEdge(context, candidate))
                    {
                        failureReason = $"Established route enters an unselected edge '{candidate.atcEdgeId}'.";
                        return false;
                    }
                }
                else
                {
                    failureReason = "Connected Yard ATC path calculation is not implemented.";
                    return false;
                }

                if (nextKey.HasValue)
                {
                    failureReason = "Multiple usable ATC branches are available.";
                    return false;
                }

                nextKey = key;
                nextRoute = route;
                nextRouteIndex = route == null ? -1 : 0;
            }

            return true;
        }

        private static bool TrySelectOriginRoute(
            TrackAtcContext context,
            TrackAtcEdgeKey key,
            out TrackAtcRouteDefinition selected,
            out int selectedIndex,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            using var originRouteScope = OriginRouteMarker.Auto();
            selected = null;
            selectedIndex = -1;
            endReason = TrackAtcPathEndReason.RouteNotSet;
            failureReason = string.Empty;
            TrackAtcRouteDefinition terminal = null;
            int terminalIndex = -1;
            var validation = context.State.validation;
            if (!validation.routeMembershipsByEdgeKey.TryGetValue(key, out var memberships))
            {
                return true;
            }

            var originEdge = validation.atcEdgesById[key.atcEdgeId];
            foreach (var membership in memberships)
            {
                var route = membership.route;
                int index = membership.routeIndex;
                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                bool canUseRoute = originEdge.controlKind == TrackAtcEdgeControlKind.Block
                    ? CanEnter(input) : CanContinue(input);
                if (!canUseRoute)
                {
                    if (input.IsRouteSet)
                    {
                        endReason = GetUnavailableReason(input);
                    }

                    continue;
                }

                if (index == route.atcEdgeIds.Count - 1)
                {
                    if (terminal != null)
                    {
                        failureReason = "Multiple usable terminal ATC routes are available.";
                        return false;
                    }

                    terminal = route;
                    terminalIndex = index;
                    continue;
                }

                if (selected != null)
                {
                    failureReason = "Multiple usable ATC routes contain the origin.";
                    return false;
                }

                selected = route;
                selectedIndex = index;
            }

            // 共有Edge自身が起点なら、開通済み後続進路内の列車も継続できる。
            if (selected == null)
            {
                selected = terminal;
                selectedIndex = terminalIndex;
            }

            return true;
        }

        private static bool TrySelectEntryRoute(
            TrackAtcContext context,
            TrackAtcEdgeKey key,
            string approachTrackCircuitId,
            string excludedAtcRouteId,
            out TrackAtcRouteDefinition selected,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            selected = null;
            endReason = TrackAtcPathEndReason.RouteNotSet;
            failureReason = string.Empty;
            string entryCircuitId = context.State.validation.atcEdgesById[key.atcEdgeId].trackCircuitId;
            bool hasBoundaryControl = false;
            if (approachTrackCircuitId != entryCircuitId)
            {
                foreach (var route in context.State.validation.atcRoutesById.Values)
                {
                    if (route.atcRouteId != excludedAtcRouteId && route.atcEdgeIds[0] == key.atcEdgeId
                        && GetRouteKey(context, route, 0) == key
                        && route.interlockingStartTrackCircuitId == approachTrackCircuitId)
                    {
                        hasBoundaryControl = true;
                        break;
                    }
                }
            }

            // 回路境界から入る制御進路がある場合、その静的な境界を優先する。
            // 同じ先頭Edgeを共有する出発進路の設定だけでは、未設定の場内を越えない。
            string controlStartCircuitId = hasBoundaryControl ? approachTrackCircuitId : entryCircuitId;
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                if (route.atcRouteId == excludedAtcRouteId || route.atcEdgeIds[0] != key.atcEdgeId ||
                    GetRouteKey(context, route, 0) != key ||
                    route.interlockingStartTrackCircuitId != controlStartCircuitId)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                if (!CanEnter(input))
                {
                    if (input.IsRouteSet)
                    {
                        endReason = GetUnavailableReason(input);
                    }

                    continue;
                }

                if (selected != null)
                {
                    failureReason = "Multiple usable entry ATC routes are available.";
                    return false;
                }

                selected = route;
            }

            return true;
        }

        private static TrackAtcPathResult Stop(
            TrackAtcContext context,
            TrackAtcPathResult result,
            TrackAtcEdgeKey stopKey,
            TrackAtcPathEndReason reason,
            HashSet<string> circuits)
        {
            var edge = context.State.validation.atcEdgesById[stopKey.atcEdgeId];
            if (!HasCircuitInput(context, edge.trackCircuitId))
            {
                return Fail(result, circuits, $"Circuit input '{edge.trackCircuitId}' is unavailable.");
            }

            if (!TryGetTerminalRoute(context, stopKey, out string terminalId, out string failureReason))
            {
                return Fail(result, circuits, failureReason);
            }

            result.isPathValid = true;
            result.stopAtcEdgeId = stopKey.atcEdgeId;
            result.stopTravelDirection = stopKey.direction;
            result.terminalAtcRouteId = terminalId;
            result.endReason = reason;
            return result;
        }

        private static bool TryGetTerminalRoute(
            TrackAtcContext context,
            TrackAtcEdgeKey stopKey,
            out string terminalId,
            out string failureReason)
        {
            var terminals = context.State.path.terminalRoutesByKey;
            if (!terminals.TryGetValue(stopKey, out var terminal) || !terminal.isCalculated)
            {
                terminal.isValid = CalculateTerminalRoute(context, stopKey,
                    out terminal.terminalAtcRouteId, out terminal.failureReason);
                terminal.isCalculated = true;
                terminals[stopKey] = terminal;
            }

            terminalId = terminal.terminalAtcRouteId;
            failureReason = terminal.failureReason;
            return terminal.isValid;
        }

        private static bool CalculateTerminalRoute(
            TrackAtcContext context,
            TrackAtcEdgeKey stopKey,
            out string terminalId,
            out string failureReason)
        {
            terminalId = null;
            failureReason = string.Empty;
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                int lastIndex = route.atcEdgeIds.Count - 1;
                if (route.atcEdgeIds[lastIndex] != stopKey.atcEdgeId ||
                    GetRouteKey(context, route, lastIndex) != stopKey)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                if (!CanContinue(input))
                {
                    continue;
                }

                if (terminalId != null)
                {
                    failureReason = "Multiple usable ATC routes correspond to the stop limit.";
                    return false;
                }

                terminalId = route.atcRouteId;
            }

            return true;
        }

        private static void ValidateNextEdgeChains(TrackAtcContext context)
        {
            // Stateには次Edgeだけを保存するため、各起点の停止限界へその参照で
            // 到達できることを工程3で確認する。途中の起点の停止限界は採用しない。
            bool changed;
            do
            {
                changed = false;
                foreach (var pair in context.State.path.resultsByKey)
                {
                    if (!pair.Value.isPathValid)
                    {
                        continue;
                    }

                    if (!TryValidateChain(context, pair.Key, pair.Value, out var circuits, out var failureReason))
                    {
                        Fail(pair.Value, circuits, failureReason);
                        changed = true;
                    }
                }
            }
            while (changed);
        }

        private static bool TryValidateChain(
            TrackAtcContext context,
            TrackAtcEdgeKey origin,
            TrackAtcPathResult originResult,
            out HashSet<string> circuits,
            out string failureReason)
        {
            circuits = new HashSet<string>();
            failureReason = string.Empty;
            var visited = new HashSet<TrackAtcEdgeKey>();
            var stopKey = new TrackAtcEdgeKey(originResult.stopAtcEdgeId, originResult.stopTravelDirection);
            var current = origin;
            for (int step = 0; step < MaximumExplorationSteps; step++)
            {
                if (!context.State.validation.atcEdgesById.TryGetValue(current.atcEdgeId, out var edge))
                {
                    failureReason = "Next-edge chain references an unavailable edge.";
                    return false;
                }

                circuits.Add(edge.trackCircuitId);
                if (current == stopKey)
                {
                    return true;
                }

                context.State.path.resultsByKey.TryGetValue(current, out var result);
                if (!visited.Add(current) || result == null ||
                    !result.isPathValid || !result.nextEdgeKey.HasValue)
                {
                    if (result != null && !result.isPathValid)
                    {
                        circuits.UnionWith(result.affectedCircuitIds);
                    }

                    failureReason = "Next-edge chain cannot reach the origin's stop limit.";
                    return false;
                }

                current = result.nextEdgeKey.Value;
            }

            failureReason = "Next-edge chain exceeded its step limit.";
            return false;
        }

        private static TrackAtcPathResult Fail(
            TrackAtcPathResult result,
            HashSet<string> circuits,
            string failureReason)
        {
            result.isPathValid = false;
            result.failureReason = failureReason;
            result.endReason = TrackAtcPathEndReason.CalculationFailed;
            result.affectedCircuitIds.UnionWith(circuits);
            return result;
        }

        private static bool TryGetRouteInput(
            TrackAtcContext context,
            TrackAtcRouteDefinition route,
            out TrackAtcRouteInput input,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!context.State.validation.hasRouteInputById.TryGetValue(route.InterlockingKey, out bool available) ||
                !available || !context.Input.RoutesById.TryGetValue(route.InterlockingKey, out input) || input == null)
            {
                input = null;
                failureReason = $"Route input '{route.interlockingStationId}/{route.interlockingRouteId}' is unavailable.";
                return false;
            }

            return true;
        }

        private static bool HasCircuitInput(TrackAtcContext context, string circuitId)
        {
            return context.State.validation.hasCircuitInputById.TryGetValue(circuitId, out bool available) && available;
        }

        private static bool IsSelectedEdge(TrackAtcContext context, TrackAtcGraphEdge edge)
        {
            return !context.State.path.selectionFailureByCircuitId.ContainsKey(edge.trackCircuitId) &&
                context.State.path.selectedEdgeByCircuitId.TryGetValue(edge.trackCircuitId, out string selectedId) &&
                selectedId == edge.atcEdgeId;
        }

        private static bool CanContinue(TrackAtcRouteInput input)
        {
            return input.IsRouteSet && input.PathEstablished && input.RouteLocked && !input.CancelPending;
        }

        private static bool CanEnter(TrackAtcRouteInput input)
        {
            return input.IsRouteSet && input.ProceedAllowed && input.PathEstablished && !input.CancelPending;
        }

        private static TrackAtcPathEndReason GetUnavailableReason(TrackAtcRouteInput input)
        {
            if (!input.IsRouteSet)
            {
                return TrackAtcPathEndReason.RouteNotSet;
            }

            return input.CancelPending ? TrackAtcPathEndReason.RouteCancelled : TrackAtcPathEndReason.RouteUnavailable;
        }

        private static TrackAtcEdgeKey GetRouteKey(
            TrackAtcContext context,
            TrackAtcRouteDefinition route,
            int index)
        {
            var edges = context.State.validation.atcEdgesById;
            var edge = edges[route.atcEdgeIds[index]];
            if (route.atcEdgeIds.Count == 1)
            {
                var singleDirection = route.entryDirection == TrackAtcTravelDirection.AtoB
                    ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA;
                return new TrackAtcEdgeKey(edge.atcEdgeId, singleDirection);
            }

            var adjacent = edges[route.atcEdgeIds[index < route.atcEdgeIds.Count - 1 ? index + 1 : index - 1]];
            bool sharedA = edge.atcNodeAId == adjacent.atcNodeAId || edge.atcNodeAId == adjacent.atcNodeBId;
            var direction = index < route.atcEdgeIds.Count - 1
                ? (sharedA ? TrackEdgeTravelDirection.BtoA : TrackEdgeTravelDirection.AtoB)
                : (sharedA ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA);
            return new TrackAtcEdgeKey(edge.atcEdgeId, direction);
        }
    }
}
