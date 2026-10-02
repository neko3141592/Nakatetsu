using System;
using System.Collections.Generic;
using System.Globalization;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
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
            IReadOnlyList<TrackInterlockingDefinition> interlockings,
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
            public readonly List<TrackAtcGraphEdge> AtcEdges = new();
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

        private sealed partial class Compilation
        {
            private readonly TrackGraphDefinition track;
            private readonly TrackGraphContext graph;
            private readonly TrackAtcGraphCompileDefinition source;
            private readonly IReadOnlyList<TrackInterlockingDefinition> interlockings;
            private readonly Dictionary<string, EdgeWork> edges = new();
            private readonly Dictionary<string, TrackAtcGraphNode> nodes = new();
            private readonly HashSet<string> routedEdgeIds = new();

            public string Error { get; private set; }
            public TrackAtcGraphDefinition Result { get; }

            public Compilation(TrackGraphDefinition track, TrackGraphContext graph,
                TrackAtcGraphCompileDefinition source, IReadOnlyList<TrackInterlockingDefinition> interlockings)
            {
                this.track = track;
                this.graph = graph;
                this.source = source;
                this.interlockings = interlockings;
                Result = new TrackAtcGraphDefinition { atcGraphId = source.atcGraphId };
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
                    if (node.connectedEdgeIds == null || node.connectedEdgeIds.Count <= 1) continue;
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

                        var atcEdge = new TrackAtcGraphEdge
                        {
                            atcEdgeId = $"edge:{Id(work.Edge.edgeId)}:{Number(start)}:{Number(end)}",
                            trackEdgeId = work.Edge.edgeId,
                            startDistanceOnEdgeM = start,
                            endDistanceOnEdgeM = end,
                            lengthM = end - start,
                            trackCircuitId = setting.trackCircuitId,
                            controlKind = setting.controlKind,
                            atcNodeAId = NodeId(work.Edge, start),
                            atcNodeBId = NodeId(work.Edge, end)
                        };
                        BuildSpeedProfile(work, atcEdge, setting.speedLimitKmh);
                        if (!BuildGradientProfile(work, atcEdge))
                        {
                            return false;
                        }

                        work.AtcEdges.Add(atcEdge);
                        Result.atcEdge.Add(atcEdge);
                        AddNodeEdge(atcEdge.atcNodeAId, atcEdge.atcEdgeId);
                        AddNodeEdge(atcEdge.atcNodeBId, atcEdge.atcEdgeId);
                    }
                }
                return true;
            }

            private static string NodeId(TrackEdgeDefinition edge, float distance)
            {
                if (distance == 0f) return $"node:{Id(edge.nodeAId)}";
                if (distance == edge.LengthM) return $"node:{Id(edge.nodeBId)}";
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
                    if (routedEdgeIds.Contains(edge.atcEdgeId))
                    {
                        edge.direction = TrackAtcTravelDirection.Unspecified;
                        continue;
                    }

                    // 分岐ノードの接続リストだけでは、定位・反位の通行ペアを表せない。
                    // 分岐直近の区間は必ず連動進路へ含め、進路に沿って探索させる。
                    if (nodes[edge.atcNodeAId].connectedAtcEdgeIds.Count > 2
                        || nodes[edge.atcNodeBId].connectedAtcEdgeIds.Count > 2)
                    {
                        return Fail($"Branch edge '{edge.atcEdgeId}' must belong to an ATC route.");
                    }

                    switch (edges[edge.trackEdgeId].Edge.travelDirection)
                    {
                        case TrackEdgeTravelDirection.AtoB:
                            edge.direction = TrackAtcTravelDirection.AtoB;
                            break;
                        case TrackEdgeTravelDirection.BtoA:
                            edge.direction = TrackAtcTravelDirection.BtoA;
                            break;
                        default:
                            return Fail($"Non-route edge '{edge.trackEdgeId}' needs travelDirection AtoB or BtoA.");
                    }
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
