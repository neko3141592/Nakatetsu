using System;
using System.Collections.Generic;
using System.Globalization;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    public static partial class TrackAtcGraphCompiler
    {
        // 入力は変更しない。成功時だけ新しい定義を返し、失敗時のresultはnullとする。
        // Track Edgeの距離表は、TrackGraphCompilerで事前に生成・反映しておく。
        public static bool TryCompile(TrackGraphDefinition track,
            TrackAtcGraphCompileDefinition source,
            IReadOnlyList<TrackStationInterlockingDefinition> interlockings,
            out TrackAtcGraphDefinition result, List<string> errors)
        {
            result = null;
            if (errors == null)
            {
                return false;
            }

            errors.Clear();
            if (source == null || string.IsNullOrWhiteSpace(source.atcGraphId)
                || source.circuits == null || source.speedLimits == null || source.routes == null
                || !IsFinite(source.gradientSampleIntervalM) || source.gradientSampleIntervalM <= 0f)
            {
                errors.Add("ATC compile definition requires an ID, lists and a positive gradient sample interval.");
                return false;
            }

            if (!IsFinite(source.maximumOperatingSpeedKmh) || source.maximumOperatingSpeedKmh < 0f)
            {
                errors.Add("ATC compile definition requires a finite, nonnegative maximum operating speed.");
                return false;
            }

            var graph = new TrackGraphContext();
            if (!TrackGraphCompiler.TryCompile(track, graph, errors))
            {
                return false;
            }

            var compilation = new Compilation(track, graph, source, interlockings);
            if (!compilation.Run())
            {
                errors.Add(compilation.Error);
                return false;
            }

            result = compilation.Result;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Id(string value) => Uri.EscapeDataString(value);

        private sealed class EdgeWork
        {
            public TrackEdgeDefinition Edge;
            public readonly List<float> Cuts = new();
            public readonly List<CircuitSpan> Circuits = new();
            public readonly List<SpeedSpan> SpeedLimits = new();

        }

        private sealed class CircuitSpan
        {
            public float Start;
            public float End;
            public TrackAtcCircuitSetting Setting;
        }

        private sealed class SpeedSpan
        {
            public float Start;
            public float End;
            public float SpeedKmh;
        }

        private sealed class Segment
        {
            public EdgeWork Work;
            public TrackAtcCircuitSetting Setting;
            public float Start;
            public float End;
            public string NodeA;
            public string NodeB;
        }

        private sealed partial class Compilation
        {
            private readonly TrackGraphDefinition track;
            private readonly TrackGraphContext graph;
            private readonly TrackAtcGraphCompileDefinition source;
            private readonly IReadOnlyList<TrackStationInterlockingDefinition> interlockings;
            private readonly Dictionary<string, EdgeWork> edges = new();
            private readonly Dictionary<string, TrackAtcGraphNode> nodes = new();
            private readonly HashSet<string> routedEdgeIds = new();
            private readonly Dictionary<string, List<Segment>> segmentsByNode = new();
            private readonly Dictionary<string, List<Segment>> segmentsByCircuit = new();
            private readonly Dictionary<string, List<TrackAtcGraphEdge>> atcEdgesByCircuit = new();

            public string Error { get; private set; }
            public TrackAtcGraphDefinition Result { get; }

            public Compilation(TrackGraphDefinition track, TrackGraphContext graph,
                TrackAtcGraphCompileDefinition source, IReadOnlyList<TrackStationInterlockingDefinition> interlockings)
            {
                this.track = track;
                this.graph = graph;
                this.source = source;
                this.interlockings = interlockings;
                Result = new TrackAtcGraphDefinition
                {
                    atcGraphId = source.atcGraphId,
                    maximumOperatingSpeedKmh = source.maximumOperatingSpeedKmh
                };
            }

            public bool Run()
            {
                return ReadEdges() && ReadCircuits() && ReadRoutes() && ReadSpeedLimits()
                    && BuildEdges() && BuildRoutes() && SetDirections();
            }

            private bool Fail(string message)
            {
                Error = message;
                return false;
            }

            private bool ReadEdges()
            {
                foreach (var node in track.nodes)
                {
                    if (node.connectedEdgeIds == null || node.connectedEdgeIds.Count <= 1)
                    {
                        continue;
                    }
                    if (!graph.TryGetConnectionAtNode(node.nodeId, out var connection))
                    {
                        return Fail($"Node '{node.nodeId}' requires a Connection to join its edges.");
                    }
                    foreach (var edgeId in node.connectedEdgeIds)
                    {
                        if (!connection.edgePairs.Exists(pair => pair.edgeAId == edgeId || pair.edgeBId == edgeId))
                        {
                            return Fail($"Node '{node.nodeId}' lists edge '{edgeId}' outside its Connection pairs.");
                        }
                    }
                }

                foreach (var edge in track.edges)
                {
                    var map = edge.distanceMap;
                    if (edge.nodeAId == edge.nodeBId || map == null || map.Count < 2
                        || map[0].distanceOnEdgeM != 0f
                        || map[0].distanceOnGeometryM != edge.startDistanceOnGeometryM
                        || map[^1].distanceOnGeometryM != edge.endDistanceOnGeometryM)
                    {
                        return Fail($"Edge '{edge.edgeId}' needs distinct endpoints and a baked distance map matching its range.");
                    }

                    bool increasing = edge.endDistanceOnGeometryM > edge.startDistanceOnGeometryM;
                    for (int i = 0; i < map.Count; i++)
                    {
                        var sample = map[i];
                        if (!IsFinite(sample.distanceOnEdgeM) || !IsFinite(sample.distanceOnGeometryM)
                            || (i > 0 && (sample.distanceOnEdgeM <= map[i - 1].distanceOnEdgeM
                                || (increasing ? sample.distanceOnGeometryM <= map[i - 1].distanceOnGeometryM
                                    : sample.distanceOnGeometryM >= map[i - 1].distanceOnGeometryM))))
                        {
                            return Fail($"Edge '{edge.edgeId}' has an invalid distance map. Rebuild it before compiling ATC.");
                        }
                    }

                    var work = new EdgeWork { Edge = edge };
                    work.Cuts.Add(0f);
                    work.Cuts.Add(edge.LengthM);
                    edges.Add(edge.edgeId, work);
                }

                return edges.Count > 0 || Fail("ATC graph requires at least one track edge.");
            }

            private bool ReadCircuits()
            {
                var settings = new Dictionary<string, TrackAtcCircuitSetting>();
                foreach (var setting in source.circuits)
                {
                    if (setting == null || !graph.TryGetCircuit(setting.trackCircuitId, out _)
                        || !Enum.IsDefined(typeof(TrackAtcEdgeControlKind), setting.controlKind)
                        || setting.controlKind == TrackAtcEdgeControlKind.Unspecified
                        || !IsFinite(setting.speedLimitKmh) || setting.speedLimitKmh < 0f)
                    {
                        return Fail("Each ATC circuit setting needs an existing circuit, control kind and nonnegative speed limit.");
                    }
                    if (!settings.TryAdd(setting.trackCircuitId, setting))
                    {
                        return Fail($"Duplicate ATC circuit setting '{setting.trackCircuitId}'.");
                    }
                }

                foreach (var circuit in graph.Circuits)
                {
                    if (!settings.TryGetValue(circuit.circuitId, out var setting)
                        || circuit.sections == null || circuit.sections.Count == 0)
                    {
                        return Fail($"Circuit '{circuit.circuitId}' needs ATC settings and at least one section.");
                    }

                    foreach (var section in circuit.sections)
                    {
                        if (section == null)
                        {
                            return Fail($"Circuit '{circuit.circuitId}' has a null section.");
                        }
                        if (!TryRange(section.edgeId, section.startDistanceOnGeometryM,
                            section.endDistanceOnGeometryM, out var work, out float a, out float b))
                        {
                            return false;
                        }

                        work.Circuits.Add(new CircuitSpan
                        {
                            Start = Math.Min(a, b), End = Math.Max(a, b), Setting = setting
                        });
                        work.Cuts.Add(a);
                        work.Cuts.Add(b);
                    }
                }

                return true;
            }

            // Geometry距離は入力座標。生成物の距離は必ず距離表で求めたEdge実距離。
            private bool TryRange(string edgeId, float startGeometry, float endGeometry,
                out EdgeWork work, out float start, out float end)
            {
                work = null;
                start = end = 0f;
                if (string.IsNullOrWhiteSpace(edgeId) || !edges.TryGetValue(edgeId, out work)
                    || !work.Edge.TryConvertToEdgeDistance(startGeometry, out start)
                    || !work.Edge.TryConvertToEdgeDistance(endGeometry, out end) || start == end)
                {
                    return Fail($"Invalid Geometry range [{startGeometry}, {endGeometry}] on edge '{edgeId}'.");
                }
                return true;
            }

            private bool BuildEdges()
            {
                if (!BuildSegments())
                {
                    return false;
                }

                var circuitIds = new List<string>(segmentsByCircuit.Keys);
                circuitIds.Sort(StringComparer.Ordinal);
                foreach (string circuitId in circuitIds)
                {
                    var paths = FindCircuitPaths(segmentsByCircuit[circuitId]);
                    if (paths.Count == 0)
                    {
                        return Fail($"Circuit '{circuitId}' has no boundary-to-boundary path.");
                    }

                    OrientCircuitPaths(paths);
                    paths.Sort((a, b) => string.CompareOrdinal(PathId(a), PathId(b)));
                    var covered = new HashSet<Segment>();
                    var compiledEdges = new List<TrackAtcGraphEdge>();
                    for (int i = 0; i < paths.Count; i++)
                    {
                        var path = paths[i];
                        var edge = new TrackAtcGraphEdge
                        {
                            atcEdgeId = paths.Count == 1 ? $"edge:{Id(circuitId)}" : $"edge:{Id(circuitId)}:{i}",
                            trackCircuitId = circuitId,
                            controlKind = path[0].Segment.Setting.controlKind,
                            atcNodeAId = path[0].EntryNode,
                            atcNodeBId = path[^1].ExitNode
                        };

                        foreach (var part in path)
                        {
                            covered.Add(part.Segment);
                            var span = new TrackAtcPhysicalSpan
                            {
                                trackEdgeId = part.Segment.Work.Edge.edgeId,
                                startDistanceOnEdgeM = part.Forward ? part.Segment.Start : part.Segment.End,
                                endDistanceOnEdgeM = part.Forward ? part.Segment.End : part.Segment.Start
                            };
                            BuildSpeedProfile(part.Segment.Work, span, edge, part.Segment.Setting.speedLimitKmh);
                            if (!BuildGradientProfile(part.Segment.Work, span, edge))
                            {
                                return false;
                            }
                            edge.lengthM += Math.Abs(span.endDistanceOnEdgeM - span.startDistanceOnEdgeM);

                            // 同じ物理Edgeの連続区間は一つにし、初期位置の共有境界を重複させない。
                            var previous = edge.physicalSpans.Count > 0 ? edge.physicalSpans[^1] : null;
                            if (previous != null && previous.trackEdgeId == span.trackEdgeId &&
                                previous.endDistanceOnEdgeM == span.startDistanceOnEdgeM &&
                                (previous.endDistanceOnEdgeM > previous.startDistanceOnEdgeM) ==
                                (span.endDistanceOnEdgeM > span.startDistanceOnEdgeM))
                            {
                                previous.endDistanceOnEdgeM = span.endDistanceOnEdgeM;
                            }
                            else
                            {
                                edge.physicalSpans.Add(span);
                            }
                        }

                        compiledEdges.Add(edge);
                        Result.atcEdge.Add(edge);
                        AddNodeEdge(edge.atcNodeAId, edge.atcEdgeId);
                        AddNodeEdge(edge.atcNodeBId, edge.atcEdgeId);
                    }
                    if (covered.Count != segmentsByCircuit[circuitId].Count)
                    {
                        return Fail($"Circuit '{circuitId}' has sections outside its traversable paths.");
                    }
                    atcEdgesByCircuit.Add(circuitId, compiledEdges);
                }
                return true;
            }

            private bool BuildSegments()
            {
                var orderedEdges = new List<EdgeWork>(edges.Values);
                orderedEdges.Sort((a, b) => string.CompareOrdinal(a.Edge.edgeId, b.Edge.edgeId));
                foreach (var work in orderedEdges)
                {
                    SortUnique(work.Cuts);
                    for (int i = 1; i < work.Cuts.Count; i++)
                    {
                        float start = work.Cuts[i - 1];
                        float end = work.Cuts[i];
                        TrackAtcCircuitSetting setting = null;
                        foreach (var section in work.Circuits)
                        {
                            if (section.Start > start || section.End < end)
                            {
                                continue;
                            }
                            if (setting != null && setting.trackCircuitId != section.Setting.trackCircuitId)
                            {
                                return Fail($"Different circuits overlap on edge '{work.Edge.edgeId}' at [{start}, {end}].");
                            }
                            setting = section.Setting;
                        }
                        if (setting == null)
                        {
                            return Fail($"No track circuit covers edge '{work.Edge.edgeId}' at [{start}, {end}].");
                        }

                        var segment = new Segment
                        {
                            Work = work,
                            Setting = setting,
                            Start = start,
                            End = end,
                            NodeA = NodeId(work.Edge, start),
                            NodeB = NodeId(work.Edge, end)
                        };
                        if (!segmentsByCircuit.TryGetValue(setting.trackCircuitId, out var circuitSegments))
                        {
                            circuitSegments = new List<Segment>();
                            segmentsByCircuit.Add(setting.trackCircuitId, circuitSegments);
                        }
                        circuitSegments.Add(segment);
                        AddSegment(segment.NodeA, segment);
                        AddSegment(segment.NodeB, segment);
                    }
                }
                return true;
            }

            private void AddSegment(string nodeId, Segment segment)
            {
                if (!segmentsByNode.TryGetValue(nodeId, out var atNode))
                {
                    atNode = new List<Segment>();
                    segmentsByNode.Add(nodeId, atNode);
                }
                atNode.Add(segment);
            }

            private sealed class PathPart
            {
                public Segment Segment;
                public bool Forward;
                public string EntryNode => Forward ? Segment.NodeA : Segment.NodeB;
                public string ExitNode => Forward ? Segment.NodeB : Segment.NodeA;
            }

            // 同じ回路内を接続ペアに沿って結合する。転轍機の共通側は定位・反位の両経路に含まれる。
            private List<List<PathPart>> FindCircuitPaths(List<Segment> segments)
            {
                var paths = new List<List<PathPart>>();
                var pathIds = new HashSet<string>();
                foreach (var segment in segments)
                {
                    TryStart(segment, true);
                    TryStart(segment, false);
                }
                return paths;

                void TryStart(Segment segment, bool forward)
                {
                    string entry = forward ? segment.NodeA : segment.NodeB;
                    if (HasCircuitContinuation(segment, entry))
                    {
                        return;
                    }
                    Walk(new PathPart { Segment = segment, Forward = forward }, new List<PathPart>(), new HashSet<Segment>());
                }

                void Walk(PathPart part, List<PathPart> path, HashSet<Segment> visited)
                {
                    path.Add(part);
                    visited.Add(part.Segment);
                    bool continued = false;
                    foreach (var next in segmentsByNode[part.ExitNode])
                    {
                        if (next == part.Segment || next.Setting.trackCircuitId != part.Segment.Setting.trackCircuitId
                            || !CanJoinSegments(part.Segment, next, part.ExitNode))
                        {
                            continue;
                        }
                        continued = true;
                        if (!visited.Contains(next))
                        {
                            Walk(new PathPart { Segment = next, Forward = next.NodeA == part.ExitNode }, path, visited);
                        }
                    }
                    if (!continued)
                    {
                        string id = PathId(path);
                        var reverse = ReversePath(path);
                        string reverseId = PathId(reverse);
                        string canonical = string.CompareOrdinal(id, reverseId) <= 0 ? id : reverseId;
                        if (pathIds.Add(canonical))
                        {
                            paths.Add(new List<PathPart>(path));
                        }
                    }
                    visited.Remove(part.Segment);
                    path.RemoveAt(path.Count - 1);
                }
            }

            private bool HasCircuitContinuation(Segment segment, string nodeId)
            {
                foreach (var other in segmentsByNode[nodeId])
                {
                    if (other != segment && other.Setting.trackCircuitId == segment.Setting.trackCircuitId
                        && CanJoinSegments(segment, other, nodeId))
                    {
                        return true;
                    }
                }
                return false;
            }

            private bool CanJoinSegments(Segment a, Segment b, string nodeId)
            {
                if (a.Work == b.Work)
                {
                    return true;
                }
                string physicalNode = Endpoint(a.Work, a.NodeA == nodeId ? a.Start : a.End);
                if (physicalNode == null || !graph.TryGetConnectionAtNode(physicalNode, out var connection))
                {
                    return false;
                }
                foreach (var pair in connection.edgePairs)
                {
                    if (MatchesPair(pair, a.Work.Edge.edgeId, b.Work.Edge.edgeId))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool MatchesPair(TrackEdgePair pair, string a, string b)
            {
                return (pair.edgeAId == a && pair.edgeBId == b) || (pair.edgeAId == b && pair.edgeBId == a);
            }

            private static string PathId(List<PathPart> path)
            {
                var parts = new List<string>();
                foreach (var part in path)
                {
                    parts.Add($"{Id(part.Segment.Work.Edge.edgeId)}:{Number(part.Forward ? part.Segment.Start : part.Segment.End)}:{Number(part.Forward ? part.Segment.End : part.Segment.Start)}");
                }
                return string.Join("/", parts);
            }

            private static List<PathPart> ReversePath(List<PathPart> path)
            {
                var reverse = new List<PathPart>();
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    reverse.Add(new PathPart { Segment = path[i].Segment, Forward = !path[i].Forward });
                }
                return reverse;
            }

            private static void OrientCircuitPaths(List<List<PathPart>> paths)
            {
                // 共有する共通区間を同じ向きにする。経路名・定位反位の接尾辞には依存しない。
                var counts = new Dictionary<Segment, int>();
                foreach (var path in paths)
                {
                    foreach (var part in path)
                    {
                        counts.TryGetValue(part.Segment, out int count);
                        counts[part.Segment] = count + 1;
                    }
                }
                var ordered = new List<Segment>(counts.Keys);
                ordered.Sort((a, b) =>
                {
                    int comparison = counts[b].CompareTo(counts[a]);
                    if (comparison == 0)
                    {
                        comparison = string.CompareOrdinal(a.Work.Edge.edgeId, b.Work.Edge.edgeId);
                    }
                    return comparison != 0 ? comparison : a.Start.CompareTo(b.Start);
                });
                for (int i = 0; i < paths.Count; i++)
                {
                    foreach (var anchor in ordered)
                    {
                        var part = paths[i].Find(item => item.Segment == anchor);
                        if (part == null)
                        {
                            continue;
                        }
                        if (!part.Forward)
                        {
                            paths[i] = ReversePath(paths[i]);
                        }
                        break;
                    }
                }
            }

            private static string NodeId(TrackEdgeDefinition edge, float distance)
            {
                if (distance == 0f)
                {
                    return $"node:{Id(edge.nodeAId)}";
                }
                if (distance == edge.LengthM)
                {
                    return $"node:{Id(edge.nodeBId)}";
                }
                return $"boundary:{Id(edge.edgeId)}:{Number(distance)}";
            }

            private void AddNodeEdge(string nodeId, string edgeId)
            {
                if (!nodes.TryGetValue(nodeId, out var node))
                {
                    node = new TrackAtcGraphNode { atcNodeId = nodeId };
                    nodes.Add(nodeId, node);
                    Result.atcNode.Add(node);
                }
                node.connectedAtcEdgeIds.Add(edgeId);
            }

            private bool SetDirections()
            {
                foreach (var edge in Result.atcEdge)
                {
                    if (edge.controlKind != TrackAtcEdgeControlKind.Block && routedEdgeIds.Contains(edge.atcEdgeId))
                    {
                        edge.direction = TrackAtcTravelDirection.Unspecified;
                        continue;
                    }

                    TrackAtcTravelDirection direction = TrackAtcTravelDirection.Unspecified;
                    foreach (var span in edge.physicalSpans)
                    {
                        var physicalDirection = edges[span.trackEdgeId].Edge.travelDirection;
                        if (physicalDirection != TrackEdgeTravelDirection.AtoB && physicalDirection != TrackEdgeTravelDirection.BtoA)
                        {
                            return Fail($"Non-route edge '{span.trackEdgeId}' needs travelDirection AtoB or BtoA.");
                        }
                        bool followsSpan = (physicalDirection == TrackEdgeTravelDirection.AtoB)
                            == (span.endDistanceOnEdgeM > span.startDistanceOnEdgeM);
                        var spanDirection = followsSpan ? TrackAtcTravelDirection.AtoB : TrackAtcTravelDirection.BtoA;
                        if (direction != TrackAtcTravelDirection.Unspecified && direction != spanDirection)
                        {
                            return Fail($"Circuit '{edge.trackCircuitId}' has inconsistent non-route travel directions.");
                        }
                        direction = spanDirection;
                    }
                    edge.direction = direction;
                }
                return true;
            }

            private static void SortUnique(List<float> values)
            {
                values.Sort();
                int count = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    if (count == 0 || values[i] != values[count - 1])
                    {
                        values[count++] = values[i];
                    }
                }
                values.RemoveRange(count, values.Count - count);
            }
        }
    }
}
