using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Atc
{
    public static partial class TrackAtcGraphCompiler
    {
        private sealed class RouteSpan
        {
            public EdgeWork Work;
            public float Entry;
            public float Exit;
        }

        private sealed class RouteWork
        {
            public TrackAtcRouteSourceDefinition Source;
            public readonly Dictionary<string, TrackSwitchPosition> Turnouts = new();
            public readonly List<RouteSpan> Path = new();
        }

        private sealed partial class Compilation
        {
            private readonly List<RouteWork> routes = new();

            private bool ReadRoutes()
            {
                var interlockingRoutes = new Dictionary<string, TrackInterlockingRouteDefinition>();
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
                        || !routeIds.Add(route.atcRouteId) || route.path == null || route.path.Count == 0)
                    {
                        return Fail("ATC routes need a unique ID and a nonempty ordered path.");
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

                    foreach (var span in route.path)
                    {
                        if (span == null) return Fail($"Route '{route.atcRouteId}' has a null path span.");
                        if (!TryRange(span.trackEdgeId, span.entryDistanceOnGeometryM,
                            span.exitDistanceOnGeometryM, out var edge, out float entry, out float exit))
                        {
                            return false;
                        }
                        var converted = new RouteSpan { Work = edge, Entry = entry, Exit = exit };
                        if (work.Path.Count > 0 && !CanFollow(work, work.Path[^1], converted))
                        {
                            return false;
                        }

                        // 進路はATC Edge列なので、進路の入口・出口も分割点にする。
                        edge.Cuts.Add(entry);
                        edge.Cuts.Add(exit);
                        work.Path.Add(converted);
                    }
                    routes.Add(work);
                }
                routes.Sort((a, b) => string.CompareOrdinal(a.Source.atcRouteId, b.Source.atcRouteId));
                return true;
            }

            private bool CanFollow(RouteWork route, RouteSpan previous, RouteSpan next)
            {
                string routeId = route.Source.atcRouteId;
                if (previous.Work == next.Work)
                {
                    return (previous.Exit == next.Entry
                        && (previous.Exit > previous.Entry) == (next.Exit > next.Entry))
                        || Fail($"Route '{routeId}' has a gap or reverses on edge '{next.Work.Edge.edgeId}'.");
                }

                string exitNode = Endpoint(previous.Work, previous.Exit);
                string entryNode = Endpoint(next.Work, next.Entry);
                if (exitNode == null || entryNode != exitNode
                    || !graph.TryGetConnectionAtNode(exitNode, out var connection))
                {
                    return Fail($"Route '{routeId}' cannot connect '{previous.Work.Edge.edgeId}' to '{next.Work.Edge.edgeId}'.");
                }

                foreach (var pair in connection.edgePairs)
                {
                    string a = previous.Work.Edge.edgeId;
                    string b = next.Work.Edge.edgeId;
                    if (!((pair.edgeAId == a && pair.edgeBId == b) || (pair.edgeAId == b && pair.edgeBId == a)))
                    {
                        continue;
                    }
                    if (pair.condition == TrackConnectionCondition.Always) return true;
                    var requiredPosition = pair.condition == TrackConnectionCondition.Normal
                        ? TrackSwitchPosition.Normal : TrackSwitchPosition.Reverse;
                    if (route.Turnouts.TryGetValue(connection.connectionId, out var position)
                        && position == requiredPosition)
                    {
                        return true;
                    }
                }
                return Fail($"Route '{routeId}' has no permitted edge pair at connection '{connection.connectionId}' with its required turnout positions.");
            }

            private static string Endpoint(EdgeWork work, float distance)
            {
                if (distance == 0f) return work.Edge.nodeAId;
                if (distance == work.Edge.LengthM) return work.Edge.nodeBId;
                return null;
            }

            private bool BuildRoutes()
            {
                foreach (var work in routes)
                {
                    var route = new TrackAtcRouteDefinition
                    {
                        atcRouteId = work.Source.atcRouteId,
                        interlockingRouteId = work.Source.interlockingRouteId
                    };
                    var path = new List<TrackAtcGraphEdge>();
                    var seen = new HashSet<string>();
                    foreach (var span in work.Path)
                    {
                        var edgesOnTrack = span.Work.AtcEdges;
                        bool forward = span.Exit > span.Entry;
                        for (int i = 0; i < edgesOnTrack.Count; i++)
                        {
                            var edge = edgesOnTrack[forward ? i : edgesOnTrack.Count - 1 - i];
                            if (edge.startDistanceOnEdgeM < Math.Min(span.Entry, span.Exit)
                                || edge.endDistanceOnEdgeM > Math.Max(span.Entry, span.Exit))
                            {
                                continue;
                            }
                            if (!seen.Add(edge.atcEdgeId))
                            {
                                return Fail($"Route '{route.atcRouteId}' traverses ATC edge '{edge.atcEdgeId}' more than once.");
                            }
                            path.Add(edge);
                            route.atcEdgeIds.Add(edge.atcEdgeId);
                        }
                    }

                    // 出力は方向を持たない順序付きEdge列。先頭2本から入口を一意に復元できること。
                    if (path.Count < 2)
                    {
                        return Fail($"Route '{route.atcRouteId}' needs at least two ATC edges to determine its entry direction.");
                    }
                    var first = path[0];
                    var second = path[1];
                    bool sharedA = first.atcNodeAId == second.atcNodeAId || first.atcNodeAId == second.atcNodeBId;
                    bool sharedB = first.atcNodeBId == second.atcNodeAId || first.atcNodeBId == second.atcNodeBId;
                    if (sharedA == sharedB)
                    {
                        return Fail($"Route '{route.atcRouteId}' has an ambiguous entry direction.");
                    }
                    string node = sharedA ? first.atcNodeBId : first.atcNodeAId;
                    var firstSpan = work.Path[0];
                    if (node != NodeId(firstSpan.Work.Edge, firstSpan.Entry))
                    {
                        return Fail($"Route '{route.atcRouteId}' cannot represent its source entry with an ordered edge list.");
                    }
                    foreach (var edge in path)
                    {
                        if (edge.atcNodeAId == node) node = edge.atcNodeBId;
                        else if (edge.atcNodeBId == node) node = edge.atcNodeAId;
                        else return Fail($"Route '{route.atcRouteId}' is disconnected at '{edge.atcEdgeId}'.");
                        routedEdgeIds.Add(edge.atcEdgeId);
                    }
                    Result.routes.Add(route);
                }
                return true;
            }
        }
    }
}
