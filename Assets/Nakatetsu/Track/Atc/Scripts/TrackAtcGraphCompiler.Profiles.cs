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
            private static void BuildSpeedProfile(EdgeWork work, TrackAtcPhysicalSpan span, TrackAtcGraphEdge edge, float baseSpeedKmh)
            {
                float start = Math.Min(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                float end = Math.Max(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                bool forward = span.endDistanceOnEdgeM > span.startDistanceOnEdgeM;
                float offset = edge.lengthM;
                var cuts = new List<float> { start, end };
                foreach (var section in work.SpeedLimits)
                {
                    if (section.End <= start || section.Start >= end)
                    {
                        continue;
                    }
                    cuts.Add(Math.Max(start, section.Start));
                    cuts.Add(Math.Min(end, section.End));
                }
                SortUnique(cuts);

                for (int i = 1; i < cuts.Count; i++)
                {
                    int cutIndex = forward ? i : cuts.Count - i;
                    float sectionStart = cuts[cutIndex - 1];
                    float sectionEnd = cuts[cutIndex];
                    float speedKmh = baseSpeedKmh;
                    foreach (var section in work.SpeedLimits)
                    {
                        if (section.Start <= sectionStart && section.End >= sectionEnd)
                        {
                            speedKmh = Math.Min(speedKmh, section.SpeedKmh);
                        }
                    }

                    float atcStart = offset + (forward ? sectionStart - start : end - sectionEnd);
                    float atcEnd = offset + (forward ? sectionEnd - start : end - sectionStart);
                    var profile = edge.speedLimitSections;
                    if (profile.Count > 0 && profile[^1].speedLimitKmh == speedKmh)
                    {
                        profile[^1].endDistanceOnAtcEdgeM = atcEnd;
                    }
                    else
                    {
                        profile.Add(new TrackAtcSpeedLimitSection
                        {
                            startDistanceOnAtcEdgeM = atcStart,
                            endDistanceOnAtcEdgeM = atcEnd,
                            speedLimitKmh = speedKmh
                        });
                    }
                }
            }

            private bool BuildGradientProfile(EdgeWork work, TrackAtcPhysicalSpan span, TrackAtcGraphEdge edge)
            {
                double intervalCount = Math.Ceiling(Math.Abs((double)span.endDistanceOnEdgeM - span.startDistanceOnEdgeM) / source.gradientSampleIntervalM);
                if (intervalCount > 100000)
                {
                    return Fail($"Edge '{edge.atcEdgeId}' would exceed 100000 gradient samples. Increase the sample interval.");
                }

                float start = Math.Min(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                float end = Math.Max(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                bool forward = span.endDistanceOnEdgeM > span.startDistanceOnEdgeM;
                float offset = edge.lengthM;
                var positions = new List<float> { start, end };
                for (int i = 1; i < (int)intervalCount; i++)
                {
                    float distance = (float)(start + (double)i * source.gradientSampleIntervalM);
                    if (distance > start && distance < end)
                    {
                        positions.Add(distance);
                    }
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
                    if (segment == null)
                    {
                        return Fail($"Geometry '{geometry.trackGeometryId}' has a null vertical segment.");
                    }
                    AddGeometryBoundary(segment.startDistanceM);
                    AddGeometryBoundary(segment.startDistanceM + segment.lengthM);
                }
                foreach (var segment in geometry.horizontalSegments)
                {
                    if (segment == null)
                    {
                        return Fail($"Geometry '{geometry.trackGeometryId}' has a null horizontal segment.");
                    }
                    AddGeometryBoundary(segment.startDistanceM);
                    AddGeometryBoundary(segment.startDistanceM + segment.lengthM);
                }
                foreach (var segment in work.Edge.offsetSegments)
                {
                    if (segment == null)
                    {
                        return Fail($"Edge '{work.Edge.edgeId}' has a null offset segment.");
                    }
                    AddGeometryBoundary(segment.startDistanceOnGeometryM);
                    AddGeometryBoundary(segment.endDistanceOnGeometryM);
                }

                SortUnique(positions);
                for (int i = 0; i < positions.Count; i++)
                {
                    float position = positions[forward ? i : positions.Count - 1 - i];
                    if (!TrackEdgeCalculator.TryEvaluate(graph, work.Edge.edgeId, position, out var sample))
                    {
                        return Fail($"Cannot sample gradient on edge '{work.Edge.edgeId}' at {position} m.");
                    }
                    var profile = new TrackAtcGradientProfile
                    {
                        distanceOnAtcEdgeM = offset + (forward ? position - start : end - position),
                        gradientPermille = sample.GradientPermille * (forward ? 1f : -1f)
                    };
                    // 接続点は次の物理区間の値を採用し、同距離のサンプルを重複させない。
                    if (edge.gradientProfiles.Count > 0
                        && edge.gradientProfiles[^1].distanceOnAtcEdgeM == profile.distanceOnAtcEdgeM)
                    {
                        edge.gradientProfiles[^1] = profile;
                    }
                    else
                    {
                        edge.gradientProfiles.Add(profile);
                    }
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
