using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Atc
{
    public static partial class TrackAtcGraphCompiler
    {
        private sealed class RouteWork
        {
            public TrackAtcRouteSourceDefinition Source;
            public readonly Dictionary<string, TrackSwitchPosition> Turnouts = new();
        }

        private sealed partial class Compilation
        {
            private readonly List<RouteWork> routes = new();

            private bool ReadRoutes()
            {
                var interlockingRoutes = new Dictionary<string, TrackStationInterlockingRouteDefinition>();
                if (interlockings != null)
                {
                    foreach (var interlocking in interlockings)
                    {
                        if (interlocking == null || interlocking.routes == null)
                        {
                            return Fail("Interlocking definition or routes are null.");
                        }
                        foreach (var route in interlocking.routes)
                        {
                            if (route == null || string.IsNullOrWhiteSpace(route.routeId)
                                || !interlockingRoutes.TryAdd(route.routeId, route))
                            {
                                return Fail("Interlocking route IDs must be nonempty and unique across the input definitions.");
                            }
                        }
                    }
                }

                var routeIds = new HashSet<string>();
                foreach (var route in source.routes)
                {
                    if (route == null || string.IsNullOrWhiteSpace(route.atcRouteId)
                        || !routeIds.Add(route.atcRouteId) || route.trackCircuitIds == null || route.trackCircuitIds.Count == 0)
                    {
                        return Fail("ATC routes need a unique ID and a nonempty ordered circuit list.");
                    }
                    if (route.trackCircuitIds.Count == 1
                        && route.entryDirection != TrackAtcTravelDirection.AtoB
                        && route.entryDirection != TrackAtcTravelDirection.BtoA)
                    {
                        return Fail($"Single-circuit route '{route.atcRouteId}' needs an entry direction.");
                    }
                    var circuitIds = new HashSet<string>();
                    foreach (string circuitId in route.trackCircuitIds)
                    {
                        if (!graph.TryGetCircuit(circuitId, out _) || !circuitIds.Add(circuitId))
                        {
                            return Fail($"Route '{route.atcRouteId}' needs existing, nonrepeated circuit IDs.");
                        }
                    }
                    if (string.IsNullOrWhiteSpace(route.interlockingRouteId)
                        || !interlockingRoutes.TryGetValue(route.interlockingRouteId, out var interlockingRoute))
                    {
                        return Fail($"ATC route '{route.atcRouteId}' references a missing interlocking route '{route.interlockingRouteId}'.");
                    }

                    var work = new RouteWork { Source = route };
                    if (interlockingRoute.requiredTurnouts == null)
                    {
                        return Fail($"Interlocking route '{interlockingRoute.routeId}' has null turnout requirements.");
                    }
                    foreach (var required in interlockingRoute.requiredTurnouts)
                    {
                        if (required == null || !graph.TryGetConnection(required.connectionId, out var connection)
                            || connection.edgePairs.Count != 2
                            || (required.requiredPosition != TrackSwitchPosition.Normal
                                && required.requiredPosition != TrackSwitchPosition.Reverse)
                            || !work.Turnouts.TryAdd(required.connectionId, required.requiredPosition))
                        {
                            return Fail($"Route '{route.atcRouteId}' has an invalid or duplicate turnout requirement.");
                        }
                    }
                    routes.Add(work);
                }
                routes.Sort((a, b) => string.CompareOrdinal(a.Source.atcRouteId, b.Source.atcRouteId));
                return true;
            }

            private static string Endpoint(EdgeWork work, float distance)
            {
                if (distance == 0f)
                {
                    return work.Edge.nodeAId;
                }
                if (distance == work.Edge.LengthM)
                {
                    return work.Edge.nodeBId;
                }
                return null;
            }

            private bool BuildRoutes()
            {
                foreach (var work in routes)
                {
                    var solutions = new List<TrackAtcRouteDefinition>();
                    ResolveRoute(work, 0, new List<TrackAtcGraphEdge>(), new List<bool>(), solutions);
                    if (solutions.Count != 1)
                    {
                        return Fail($"Route '{work.Source.atcRouteId}' must resolve to exactly one connected path using its circuits and turnout requirements; found {solutions.Count}.");
                    }
                    var route = solutions[0];
                    foreach (string edgeId in route.atcEdgeIds)
                    {
                        routedEdgeIds.Add(edgeId);
                    }
                    Result.routes.Add(route);
                }
                return true;
            }

            // 回路の並びと連動の転轍機条件から、生成済みEdgeと先頭方向を一意に求める。
            private void ResolveRoute(RouteWork work, int index, List<TrackAtcGraphEdge> path,
                List<bool> directions, List<TrackAtcRouteDefinition> solutions)
            {
                if (solutions.Count > 1)
                {
                    return;
                }
                if (index == work.Source.trackCircuitIds.Count)
                {
                    var route = new TrackAtcRouteDefinition
                    {
                        atcRouteId = work.Source.atcRouteId,
                        interlockingRouteId = work.Source.interlockingRouteId,
                        entryDirection = directions[0] ? TrackAtcTravelDirection.AtoB : TrackAtcTravelDirection.BtoA
                    };
                    foreach (var edge in path)
                    {
                        route.atcEdgeIds.Add(edge.atcEdgeId);
                    }
                    solutions.Add(route);
                    return;
                }

                foreach (var candidate in atcEdgesByCircuit[work.Source.trackCircuitIds[index]])
                {
                    if (!CanTraverseEdge(work, candidate))
                    {
                        continue;
                    }
                    TryDirection(true);
                    TryDirection(false);

                    void TryDirection(bool forward)
                    {
                        if (work.Source.trackCircuitIds.Count == 1
                            && forward != (work.Source.entryDirection == TrackAtcTravelDirection.AtoB))
                        {
                            return;
                        }
                        if (path.Count > 0 && !CanFollowEdges(work, path[^1], directions[^1], candidate, forward))
                        {
                            return;
                        }
                        path.Add(candidate);
                        directions.Add(forward);
                        ResolveRoute(work, index + 1, path, directions, solutions);
                        directions.RemoveAt(directions.Count - 1);
                        path.RemoveAt(path.Count - 1);
                    }
                }
            }

            private bool CanTraverseEdge(RouteWork route, TrackAtcGraphEdge edge)
            {
                for (int i = 1; i < edge.physicalSpans.Count; i++)
                {
                    var previous = edge.physicalSpans[i - 1];
                    var next = edge.physicalSpans[i];
                    if (!CanFollowPhysical(route, previous.trackEdgeId, previous.endDistanceOnEdgeM,
                        next.trackEdgeId, next.startDistanceOnEdgeM))
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool CanFollowEdges(RouteWork route, TrackAtcGraphEdge previous, bool previousForward,
                TrackAtcGraphEdge next, bool nextForward)
            {
                string exitNode = previousForward ? previous.atcNodeBId : previous.atcNodeAId;
                string entryNode = nextForward ? next.atcNodeAId : next.atcNodeBId;
                if (exitNode != entryNode)
                {
                    return false;
                }
                var exitSpan = previous.physicalSpans[previousForward ? previous.physicalSpans.Count - 1 : 0];
                var entrySpan = next.physicalSpans[nextForward ? 0 : next.physicalSpans.Count - 1];
                float exit = previousForward ? exitSpan.endDistanceOnEdgeM : exitSpan.startDistanceOnEdgeM;
                float entry = nextForward ? entrySpan.startDistanceOnEdgeM : entrySpan.endDistanceOnEdgeM;
                return CanFollowPhysical(route, exitSpan.trackEdgeId, exit, entrySpan.trackEdgeId, entry);
            }

            private bool CanFollowPhysical(RouteWork route, string previousId, float exit, string nextId, float entry)
            {
                if (previousId == nextId)
                {
                    return exit == entry;
                }
                string node = Endpoint(edges[previousId], exit);
                if (node == null || node != Endpoint(edges[nextId], entry)
                    || !graph.TryGetConnectionAtNode(node, out var connection))
                {
                    return false;
                }
                foreach (var pair in connection.edgePairs)
                {
                    if (!MatchesPair(pair, previousId, nextId))
                    {
                        continue;
                    }
                    if (pair.condition == TrackConnectionCondition.Always)
                    {
                        return true;
                    }
                    var requiredPosition = pair.condition == TrackConnectionCondition.Normal
                        ? TrackSwitchPosition.Normal : TrackSwitchPosition.Reverse;
                    if (route.Turnouts.TryGetValue(connection.connectionId, out var position)
                        && position == requiredPosition)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
