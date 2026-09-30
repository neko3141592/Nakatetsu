using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Track.Atc
{
    public static partial class TrackAtcGraphCompiler
    {
        private sealed partial class Compilation
        {
            private bool ReadSpeedLimits()
            {
                foreach (var section in source.speedLimits)
                {
                    if (section == null || !IsFinite(section.speedLimitKmh) || section.speedLimitKmh < 0f)
                    {
                        return Fail("ATC speed sections require finite, nonnegative speed limits.");
                    }
                    if (!TryRange(section.trackEdgeId, section.startDistanceOnGeometryM,
                        section.endDistanceOnGeometryM, out var work, out float a, out float b))
                    {
                        return false;
                    }
                    work.SpeedLimits.Add(new SpeedSpan
                    {
                        Start = Math.Min(a, b), End = Math.Max(a, b), SpeedKmh = section.speedLimitKmh
                    });
                }
                return true;
            }

            // 速度境界ではATC Edgeを分割せず、Edge内の区間として保持する。
            private static void BuildSpeedProfile(EdgeWork work, TrackAtcGraphEdge edge, float baseSpeedKmh)
            {
                float start = edge.startDistanceOnEdgeM;
                float end = edge.endDistanceOnEdgeM;
                var cuts = new List<float> { start, end };
                foreach (var section in work.SpeedLimits)
                {
                    if (section.End <= start || section.Start >= end) continue;
                    cuts.Add(Math.Max(start, section.Start));
                    cuts.Add(Math.Min(end, section.End));
                }
                SortUnique(cuts);

                for (int i = 1; i < cuts.Count; i++)
                {
                    float speedKmh = baseSpeedKmh;
                    foreach (var section in work.SpeedLimits)
                    {
                        if (section.Start <= cuts[i - 1] && section.End >= cuts[i])
                        {
                            speedKmh = Math.Min(speedKmh, section.SpeedKmh);
                        }
                    }

                    var profile = edge.speedLimitSections;
                    if (profile.Count > 0 && profile[^1].speedLimitKmh == speedKmh)
                    {
                        profile[^1].endDistanceOnAtcEdgeM = cuts[i] - start;
                    }
                    else
                    {
                        profile.Add(new TrackAtcSpeedLimitSection
                        {
                            startDistanceOnAtcEdgeM = cuts[i - 1] - start,
                            endDistanceOnAtcEdgeM = cuts[i] - start,
                            speedLimitKmh = speedKmh
                        });
                    }
                }
            }

            private bool BuildGradientProfile(EdgeWork work, TrackAtcGraphEdge edge)
            {
                double intervalCount = Math.Ceiling((double)edge.lengthM / source.gradientSampleIntervalM);
                if (intervalCount > 100000)
                {
                    return Fail($"Edge '{edge.atcEdgeId}' would exceed 100000 gradient samples. Increase the sample interval.");
                }

                float start = edge.startDistanceOnEdgeM;
                float end = edge.endDistanceOnEdgeM;
                var positions = new List<float> { start, end };
                for (int i = 1; i < (int)intervalCount; i++)
                {
                    float distance = (float)(start + (double)i * source.gradientSampleIntervalM);
                    if (distance > start && distance < end) positions.Add(distance);
                }

                // 等間隔の点に加え、線形・横オフセットの境界も残す。
                graph.TryGetGeometry(work.Edge.geometryId, out var geometry);
                if (geometry.verticalSegments == null || geometry.horizontalSegments == null
                    || work.Edge.offsetSegments == null)
                {
                    return Fail($"Edge '{work.Edge.edgeId}' is missing geometry or offset segments.");
                }
                foreach (var segment in geometry.verticalSegments)
                {
                    if (segment == null) return Fail($"Geometry '{geometry.trackGeometryId}' has a null vertical segment.");
                    AddGeometryBoundary(segment.startDistanceM);
                    AddGeometryBoundary(segment.startDistanceM + segment.lengthM);
                }
                foreach (var segment in geometry.horizontalSegments)
                {
                    if (segment == null) return Fail($"Geometry '{geometry.trackGeometryId}' has a null horizontal segment.");
                    AddGeometryBoundary(segment.startDistanceM);
                    AddGeometryBoundary(segment.startDistanceM + segment.lengthM);
                }
                foreach (var segment in work.Edge.offsetSegments)
                {
                    if (segment == null) return Fail($"Edge '{work.Edge.edgeId}' has a null offset segment.");
                    AddGeometryBoundary(segment.startDistanceOnGeometryM);
                    AddGeometryBoundary(segment.endDistanceOnGeometryM);
                }

                SortUnique(positions);
                foreach (float position in positions)
                {
                    if (!TrackEdgeCalculator.TryEvaluate(graph, work.Edge.edgeId, position, out var sample))
                    {
                        return Fail($"Cannot sample gradient on edge '{work.Edge.edgeId}' at {position} m.");
                    }
                    edge.gradientProfiles.Add(new TrackAtcGradientProfile
                    {
                        distanceOnAtcEdgeM = position - start,
                        gradientPermille = sample.GradientPermille
                    });
                }
                return true;

                void AddGeometryBoundary(float geometryDistance)
                {
                    if (work.Edge.TryConvertToEdgeDistance(geometryDistance, out float distance)
                        && distance > start && distance < end)
                    {
                        positions.Add(distance);
                    }
                }
            }
        }
    }
}
