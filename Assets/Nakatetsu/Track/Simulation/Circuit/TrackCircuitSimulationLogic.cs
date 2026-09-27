using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Circuit;

namespace Nakatetsu.Track.Simulation.Circuit
{
    public static class TrackCircuitSimulationLogic
    {
        public static void Calculate(TrackCircuitSimulationContext context, TrackGraphContext graph,
            IReadOnlyList<TrackOccupiedEdgeSpan> occupiedEdges)
        {
            if (context == null)
            {
                return;
            }

            var state = context.State;
            state.SetAllOccupied(graph?.Circuits);
            if (graph == null || !graph.IsInitialized || occupiedEdges == null)
            {
                return;
            }

            // 入力が一つでも不正なら、全回路を占有のままにする。
            foreach (var occupied in occupiedEdges)
            {
                if (!graph.TryGetEdge(occupied.EdgeId, out var edge)
                    || !edge.TryConvertToEdgeDistance(occupied.StartDistanceOnGeometryM, out _)
                    || !edge.TryConvertToEdgeDistance(occupied.EndDistanceOnGeometryM, out _))
                {
                    return;
                }
            }

            foreach (TrackCircuitDefinition circuit in graph.Circuits)
            {
                if (circuit.sections == null || circuit.sections.Count == 0)
                {
                    return;
                }

                foreach (TrackCircuitSection section in circuit.sections)
                {
                    if (section == null
                        || section.startDistanceOnGeometryM == section.endDistanceOnGeometryM
                        || !graph.TryGetEdge(section.edgeId, out var edge)
                        || !edge.TryConvertToEdgeDistance(section.startDistanceOnGeometryM, out _)
                        || !edge.TryConvertToEdgeDistance(section.endDistanceOnGeometryM, out _))
                    {
                        return;
                    }
                }
            }

            foreach (TrackCircuitDefinition circuit in graph.Circuits)
            {
                bool occupied = false;
                foreach (TrackCircuitSection section in circuit.sections)
                {
                    foreach (var trainSpan in occupiedEdges)
                    {
                        if (trainSpan.EdgeId == section.edgeId
                            && RangesOverlap(section.startDistanceOnGeometryM,
                                section.endDistanceOnGeometryM,
                                trainSpan.StartDistanceOnGeometryM,
                                trainSpan.EndDistanceOnGeometryM))
                        {
                            occupied = true;
                            break;
                        }
                    }
                }

                state.SetOccupied(circuit.circuitId, occupied);
            }
        }

        // 境界一致も占有とみなす。
        private static bool RangesOverlap(float aStart, float aEnd, float bStart, float bEnd)
        {
            float a = Math.Min(aStart, aEnd);
            float b = Math.Max(aStart, aEnd);
            float c = Math.Min(bStart, bEnd);
            float d = Math.Max(bStart, bEnd);
            return Math.Max(a, c) <= Math.Min(b, d);
        }
    }
}
