using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public static class TrainTrackSamplingLogic
    {
        public static void ConfigureLayout(TrainTrackPositionContext context, IReadOnlyList<float> carLengthsM,
            IReadOnlyList<float> bogieCenterDistancesM)
        {
            if (carLengthsM == null || bogieCenterDistancesM == null
                || carLengthsM.Count != bogieCenterDistancesM.Count)
            {
                throw new ArgumentException("Car lengths and bogie distances must have the same count.");
            }

            var offsets = new TrainTrackCarOffsets[carLengthsM.Count];
            double totalLength = 0d;
            for (int i = 0; i < carLengthsM.Count; i++)
            {
                double center = carLengthsM[0] * 0.5d - totalLength - carLengthsM[i] * 0.5d;
                double halfBogieDistance = bogieCenterDistancesM[i] * 0.5d;
                offsets[i] = new TrainTrackCarOffsets(center,
                    center + halfBogieDistance, center - halfBogieDistance);
                totalLength += carLengthsM[i];
            }

            context.Settings.SetCarOffsets(offsets);
            context.Output.Invalidate();
        }

        // 現在の分岐接続をたどり、指定した車両上の位置をGraphから評価する。
        public static bool TryGetTrackSample(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge, int carIndex, float offsetFromCarCenterM,
            out TrainTrackSample sample)
        {
            sample = default;
            if (context == null || carIndex < 0 || carIndex >= context.Settings.CarCount
                || !TrainTrackPathLogic.IsFinite(offsetFromCarCenterM))
            {
                return false;
            }

            double offset = context.Settings.CarOffsetsArray[carIndex].CenterM + offsetFromCarCenterM;
            if (!TrainTrackPathLogic.TryLocateOffset(context, graph, resolveNextEdge, offset,
                out var edge, out float distanceOnEdgeM, out bool frontFacesAtoB)
                || !TrackEdgeCalculator.TryEvaluate(graph, edge.edgeId, distanceOnEdgeM,
                    out var trackSample))
            {
                return false;
            }

            sample = new TrainTrackSample(carIndex, offsetFromCarCenterM, edge.edgeId,
                frontFacesAtoB, trackSample);
            return true;
        }

        // 先頭車の前台車から最後尾の後台車までを、毎回Graphから再計算する。
        public static bool RefreshOutput(TrainTrackPositionContext context, TrackGraphContext graph,
            TrainTrackConnectionResolver resolveNextEdge)
        {
            if (context == null)
            {
                return false;
            }

            var output = context.Output;
            int carCount = context.Settings.CarCount;
            output.BeginUpdate(carCount);
            if (carCount == 0 || graph == null || resolveNextEdge == null)
            {
                return false;
            }

            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                var offsets = context.Settings.CarOffsetsArray[carIndex];
                float frontOffset = (float)(offsets.FrontBogieM - offsets.CenterM);
                float rearOffset = (float)(offsets.RearBogieM - offsets.CenterM);
                if (!TryGetTrackSample(context, graph, resolveNextEdge, carIndex,
                    frontOffset, out var front)
                    || !TryGetTrackSample(context, graph, resolveNextEdge, carIndex,
                        rearOffset, out var rear))
                {
                    output.Invalidate();
                    return false;
                }

                output.SetBogies(carIndex, front, rear);
            }

            if (!TryBuildOccupiedEdges(context, graph, resolveNextEdge,
                output, output.GetFrontBogie(0)))
            {
                output.Invalidate();
                return false;
            }

            output.CompleteUpdate();
            return true;
        }

        private static bool TryBuildOccupiedEdges(TrainTrackPositionContext context,
            TrackGraphContext graph, TrainTrackConnectionResolver resolveNextEdge,
            TrainTrackPositionOutput output, TrainTrackSample firstFrontBogie)
        {
            double remainingM = context.Settings.CarOffsetsArray[0].FrontBogieM
                - context.Settings.CarOffsetsArray[^1].RearBogieM;
            if (remainingM <= 0d || !graph.TryGetEdge(firstFrontBogie.EdgeId, out var edge)
                || !TrainTrackPathLogic.IsValidEdge(edge))
            {
                return false;
            }

            bool frontFacesAtoB = firstFrontBogie.FrontFacesAtoB;
            double distanceOnEdgeM = firstFrontBogie.DistanceOnEdgeM;
            for (int transitions = 0; transitions <= TrainTrackPositionLogic.guard; transitions++)
            {
                if (remainingM <= 0d)
                {
                    return output.OccupiedEdges.Count > 0;
                }

                double availableM = frontFacesAtoB ? distanceOnEdgeM : edge.LengthM - distanceOnEdgeM;
                if (availableM < 0d)
                {
                    return false;
                }

                double travelM = Math.Min(remainingM, availableM);
                double endDistanceOnEdgeM = frontFacesAtoB
                    ? distanceOnEdgeM - travelM : distanceOnEdgeM + travelM;
                if (travelM > 0d)
                {
                    if (!edge.TryConvertToGeometryDistance((float)distanceOnEdgeM, out float startGeometryM)
                        || !edge.TryConvertToGeometryDistance((float)endDistanceOnEdgeM,
                            out float endGeometryM))
                    {
                        return false;
                    }

                    output.AddOccupiedEdge(new TrackOccupiedEdgeSpan(edge.edgeId,
                        startGeometryM, endGeometryM));
                }

                remainingM -= travelM;
                if (remainingM <= 0d)
                {
                    return true;
                }

                if (transitions == TrainTrackPositionLogic.guard
                    || !TrainTrackPathLogic.TryGetAdjacent(graph, resolveNextEdge, edge,
                        frontFacesAtoB, false, out var next, out bool nextFacesAtoB,
                        out float entryDistanceM))
                {
                    return false;
                }

                edge = next;
                frontFacesAtoB = nextFacesAtoB;
                distanceOnEdgeM = entryDistanceM;
            }

            return false;
        }
    }
}
