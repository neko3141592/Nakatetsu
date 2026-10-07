using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Track.Atc
{
    internal static class TrackAtcPathLogic
    {
        private const int MaximumExplorationSteps = 512;

        internal static void UpdatePath(TrackAtcContext context)
        {
            var path = context.State.path;
            path.resultsByKey.Clear();
            if (!context.State.validation.isGraphValid)
            {
                return;
            }

            ExploreAllOrigins(context);
            ValidateNextEdgeChains(context);
        }

        private static void ExploreAllOrigins(TrackAtcContext context)
        {
            foreach (var edge in context.State.validation.atcEdgesById.Values)
            {
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
            if (originEdge.controlKind == TrackAtcEdgeControlKind.Interlocking &&
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
                return TryFindConnectedEdge(context, currentKey, true, out nextKey,
                    out nextRoute, out nextRouteIndex, out endReason, out failureReason);
            }

            if (currentRoute == null)
            {
                endReason = TrackAtcPathEndReason.RouteNotSet;
                return true;
            }

            var input = context.Input.RoutesById[currentRoute.interlockingRouteId];
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
            if (!TrySelectEntryRoute(context, currentKey, currentRoute.atcRouteId,
                out var successor, out endReason, out failureReason))
            {
                return false;
            }

            if (successor != null)
            {
                // 共有Edgeは後続進路の先頭でもある。そこから2本目へ進む際は、
                // Blockから先頭へ入った後と同じ進路内継続条件を適用する。
                var successorInput = context.Input.RoutesById[successor.interlockingRouteId];
                if (!CanContinue(successorInput))
                {
                    endReason = GetUnavailableReason(successorInput);
                    return true;
                }

                nextRoute = successor;
                nextRouteIndex = 1;
                nextKey = GetRouteKey(context, successor, nextRouteIndex);
                return true;
            }

            if (!TryFindConnectedEdge(context, currentKey, false, out nextKey,
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
            bool allowRouteEntry,
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
                var direction = candidate.atcNodeAId == exitNode
                    ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA;
                var key = new TrackAtcEdgeKey(candidateId, direction);
                TrackAtcRouteDefinition route = null;
                if (candidate.controlKind == TrackAtcEdgeControlKind.Block)
                {
                    if (!validation.directionsByAtcEdgeId[candidateId].Contains(direction))
                    {
                        continue;
                    }
                }
                else if (candidate.controlKind == TrackAtcEdgeControlKind.Interlocking)
                {
                    if (!allowRouteEntry)
                    {
                        continue;
                    }

                    if (!TrySelectEntryRoute(context, key, null, out route,
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
            selected = null;
            selectedIndex = -1;
            endReason = TrackAtcPathEndReason.RouteNotSet;
            failureReason = string.Empty;
            TrackAtcRouteDefinition terminal = null;
            int terminalIndex = -1;
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                int index = route.atcEdgeIds.IndexOf(key.atcEdgeId);
                if (index < 0 || GetRouteKey(context, route, index) != key)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                if (!CanContinue(input))
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
            string excludedAtcRouteId,
            out TrackAtcRouteDefinition selected,
            out TrackAtcPathEndReason endReason,
            out string failureReason)
        {
            selected = null;
            endReason = TrackAtcPathEndReason.RouteNotSet;
            failureReason = string.Empty;
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                if (route.atcRouteId == excludedAtcRouteId || route.atcEdgeIds[0] != key.atcEdgeId ||
                    GetRouteKey(context, route, 0) != key)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out failureReason))
                {
                    return false;
                }

                if (!input.IsRouteSet || !input.ProceedAllowed || !input.PathEstablished || input.CancelPending)
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

            string terminalId = null;
            foreach (var route in context.State.validation.atcRoutesById.Values)
            {
                int lastIndex = route.atcEdgeIds.Count - 1;
                if (route.atcEdgeIds[lastIndex] != stopKey.atcEdgeId ||
                    GetRouteKey(context, route, lastIndex) != stopKey)
                {
                    continue;
                }

                if (!TryGetRouteInput(context, route, out var input, out var failureReason))
                {
                    return Fail(result, circuits, failureReason);
                }

                if (!CanContinue(input))
                {
                    continue;
                }

                if (terminalId != null)
                {
                    return Fail(result, circuits, "Multiple usable ATC routes correspond to the stop limit.");
                }

                terminalId = route.atcRouteId;
            }

            result.isPathValid = true;
            result.stopAtcEdgeId = stopKey.atcEdgeId;
            result.stopTravelDirection = stopKey.direction;
            result.terminalAtcRouteId = terminalId;
            result.endReason = reason;
            return result;
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
            if (!context.State.validation.hasRouteInputById.TryGetValue(route.interlockingRouteId, out bool available) ||
                !available || !context.Input.RoutesById.TryGetValue(route.interlockingRouteId, out input) || input == null)
            {
                input = null;
                failureReason = $"Route input '{route.interlockingRouteId}' is unavailable.";
                return false;
            }

            return true;
        }

        private static bool HasCircuitInput(TrackAtcContext context, string circuitId)
        {
            return context.State.validation.hasCircuitInputById.TryGetValue(circuitId, out bool available) && available;
        }

        private static bool CanContinue(TrackAtcRouteInput input)
        {
            return input.IsRouteSet && input.PathEstablished && input.RouteLocked && !input.CancelPending;
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
            var adjacent = edges[route.atcEdgeIds[index < route.atcEdgeIds.Count - 1 ? index + 1 : index - 1]];
            bool sharedA = edge.atcNodeAId == adjacent.atcNodeAId || edge.atcNodeAId == adjacent.atcNodeBId;
            var direction = index < route.atcEdgeIds.Count - 1
                ? (sharedA ? TrackEdgeTravelDirection.BtoA : TrackEdgeTravelDirection.AtoB)
                : (sharedA ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA);
            return new TrackAtcEdgeKey(edge.atcEdgeId, direction);
        }
    }
}
