using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Geometry;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge
{
    public static class TrackEdgeCompiler
    {
        public const float DefaultIntegrationStepM = 0.05f;
        public const int MaxSamplesPerEdge = 1000000;

        // 評価点間の弦長を累積して距離表を作る。失敗時は部分的な距離表を返さない。
        public static bool TryBuildDistanceMap(TrackEdgeDefinition edge, TrackGeometryDefinition geometry,
            float integrationStepM, out List<TrackEdgeDistanceSample> distanceMap, out string error)
        {
            distanceMap = null;
            error = null;

            if (edge == null || geometry == null || !Finite(integrationStepM) || integrationStepM <= 0f)
            {
                return Fail("Missing definition or invalid integration step.", out error);
            }

            float start = edge.startDistanceOnGeometryM, end = edge.endDistanceOnGeometryM;
            if (!Finite(start) || !Finite(end) || start == end || Mathf.Min(start, end) < 0f
                || !Finite(geometry.lengthM) || Mathf.Max(start, end) > geometry.lengthM)
            {
                return Fail("Invalid Geometry distance range.", out error);
            }

            float min = Mathf.Min(start, end), max = Mathf.Max(start, end);
            var boundaries = new SortedSet<float> { min, max };

            if (geometry.horizontalSegments == null || geometry.horizontalSegments.Count == 0)
            {
                return Fail("Horizontal segments are missing.", out error);
            }

            float previousEnd = 0f;
            foreach (var segment in geometry.horizontalSegments)
            {
                if (segment == null || !Finite(segment.startDistanceM) || !Finite(segment.lengthM)
                    || segment.lengthM <= 0f || segment.startDistanceM != previousEnd)
                {
                    return Fail("Horizontal segments must be contiguous from zero.", out error);
                }

                previousEnd = segment.startDistanceM + segment.lengthM;
                if (!Finite(previousEnd))
                {
                    return Fail("Invalid horizontal endpoint.", out error);
                }

                AddBoundary(boundaries, previousEnd, min, max);
            }

            if (previousEnd < max)
            {
                return Fail("Horizontal segments do not cover the Edge.", out error);
            }

            if (geometry.verticalSegments != null)
            {
                foreach (var segment in geometry.verticalSegments)
                {
                    if (segment == null || !Finite(segment.startDistanceM) || !Finite(segment.lengthM))
                    {
                        return Fail("Invalid vertical segment.", out error);
                    }

                    if (segment.lengthM <= 0.001f)
                    {
                        continue;
                    }

                    AddBoundary(boundaries, segment.startDistanceM, min, max);
                    AddBoundary(boundaries, segment.startDistanceM + segment.lengthM, min, max);
                }
            }

            if (edge.offsetSegments == null || edge.offsetSegments.Count == 0)
            {
                return Fail("Offset segments are required; use an explicit zero-offset segment.", out error);
            }

            float covered = min;
            TrackEdgeOffsetSegment previous = null;
            foreach (var segment in edge.offsetSegments)
            {
                if (segment == null || !Finite(segment.startDistanceOnGeometryM) || !Finite(segment.endDistanceOnGeometryM)
                    || segment.startDistanceOnGeometryM < 0f || segment.startDistanceOnGeometryM >= segment.endDistanceOnGeometryM
                    || (previous != null && segment.startDistanceOnGeometryM < previous.endDistanceOnGeometryM))
                {
                    return Fail("Offset segments must be ordered, finite and non-overlapping.", out error);
                }

                if (previous != null && segment.startDistanceOnGeometryM == previous.endDistanceOnGeometryM
                    && segment.startDistanceOnGeometryM >= min && segment.startDistanceOnGeometryM <= max)
                {
                    float a = previous.EvaluateOffsetM(segment.startDistanceOnGeometryM);
                    float b = segment.EvaluateOffsetM(segment.startDistanceOnGeometryM);
                    if (!Finite(a) || !Finite(b) || Mathf.Abs(a - b) > 0.0001f)
                    {
                        return Fail("Offset is discontinuous at a segment boundary.", out error);
                    }
                }

                if (segment.endDistanceOnGeometryM > min && segment.startDistanceOnGeometryM < max)
                {
                    if (segment.startDistanceOnGeometryM > covered)
                    {
                        return Fail("Gap in offset coverage.", out error);
                    }

                    covered = Mathf.Max(covered, segment.endDistanceOnGeometryM);
                }

                AddBoundary(boundaries, segment.startDistanceOnGeometryM, min, max);
                AddBoundary(boundaries, segment.endDistanceOnGeometryM, min, max);
                previous = segment;
            }

            if (covered < max)
            {
                return Fail("Offset segments do not cover the Edge.", out error);
            }

            // 各区間の境界を残したまま、Geometryの向きに沿って評価点を並べる。
            var points = new List<float>(boundaries);
            if (start > end)
            {
                points.Reverse();
            }

            double estimatedSamples = 1;
            for (int i = 1; i < points.Count; i++)
            {
                estimatedSamples += Math.Ceiling(Math.Abs((double)points[i] - points[i - 1]) / integrationStepM);
            }

            if (estimatedSamples > MaxSamplesPerEdge)
            {
                return Fail($"LUT exceeds {MaxSamplesPerEdge} samples; increase the integration step.", out error);
            }

            if (!TryPosition(edge, geometry, start, out var previousPosition))
            {
                return Fail("Cannot evaluate the start of the Edge.", out error);
            }

            var result = new List<TrackEdgeDistanceSample>((int)estimatedSamples);
            result.Add(new TrackEdgeDistanceSample { distanceOnGeometryM = start, distanceOnEdgeM = 0f });
            double accumulated = 0.0;
            float previousDistance = start;

            // 隣接する評価点の直線距離をEdge実距離として積み上げる。
            for (int interval = 1; interval < points.Count; interval++)
            {
                double from = points[interval - 1], to = points[interval];
                double span = Math.Abs(to - from), direction = Math.Sign(to - from);
                int count = (int)Math.Ceiling(span / integrationStepM);
                for (int i = 1; i <= count; i++)
                {
                    float distance = i == count ? (float)to : (float)(from + direction * Math.Min(i * (double)integrationStepM, span));
                    if (distance == previousDistance)
                    {
                        return Fail("Geometry float precision cannot represent the integration step.", out error);
                    }

                    if (!TryPosition(edge, geometry, distance, out var position))
                    {
                        return Fail($"Cannot evaluate Geometry distance {distance}.", out error);
                    }

                    float chord = Vector3.Distance(previousPosition, position);
                    if (!Finite(chord) || chord <= 0f)
                    {
                        return Fail("Nonfinite or zero-length integration interval.", out error);
                    }

                    accumulated += chord;
                    float storedDistance = (float)accumulated;
                    if (!Finite(storedDistance) || storedDistance <= result[result.Count - 1].distanceOnEdgeM)
                    {
                        return Fail("Accumulated distance cannot be stored as a strictly increasing float LUT.", out error);
                    }

                    result.Add(new TrackEdgeDistanceSample { distanceOnGeometryM = distance, distanceOnEdgeM = storedDistance });
                    previousDistance = distance;
                    previousPosition = position;
                }
            }

            distanceMap = result;
            return true;
        }

        private static bool TryPosition(TrackEdgeDefinition edge, TrackGeometryDefinition geometry, float distance, out Vector3 position)
        {
            if (!TrackEdgeCalculator.TryEvaluateAtGeometryDistance(edge, geometry, distance, out position, out var derivative))
            {
                return false;
            }

            float horizontalRate = new Vector2(derivative.x, derivative.z).magnitude;
            return Finite(horizontalRate) && horizontalRate > 1e-6f;
        }

        private static void AddBoundary(SortedSet<float> boundaries, float value, float min, float max)
        {
            if (value > min && value < max)
            {
                boundaries.Add(value);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
