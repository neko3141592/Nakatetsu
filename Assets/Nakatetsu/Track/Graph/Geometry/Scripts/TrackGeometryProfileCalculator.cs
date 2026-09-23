using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    public static class TrackGeometryProfileCalculator
    {
        private const float MinSegmentLengthM = 0.001f;
        private const float PermilleScale = 1000f;

        // 高さの二階微分を返す。区間外は0、共有端点では手前側を採用する。
        public static float GetSecondDerivativeAt(List<TrackGeometryVerticalSegment> segments, float distanceOnGeometryM)
        {
            if (segments == null)
            {
                return 0f;
            }

            foreach (var segment in segments)
            {
                if (segment == null || segment.lengthM <= MinSegmentLengthM)
                {
                    continue;
                }

                float startM = Mathf.Max(0f, segment.startDistanceM);
                if (distanceOnGeometryM < startM)
                {
                    return 0f;
                }

                if (distanceOnGeometryM <= startM + segment.lengthM)
                {
                    return segment.EvaluateSecondDerivative(distanceOnGeometryM);
                }
            }

            return 0f;
        }

        public static float GetVerticalHeightAt(List<TrackGeometryVerticalSegment> segments, float distanceOnGeometryM)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            float heightM = 0f;
            float cursorDistanceM = 0f;
            float currentDerivative = 0f;
            float targetDistanceM = Mathf.Max(0f, distanceOnGeometryM);

            for (int i = 0; i < segments.Count; i++)
            {
                TrackGeometryVerticalSegment segment = segments[i];
                if (segment == null || segment.lengthM <= MinSegmentLengthM)
                {
                    continue;
                }

                float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
                float segmentEndM = segmentStartM + Mathf.Max(0f, segment.lengthM);

                if (targetDistanceM <= segmentStartM)
                {
                    heightM += currentDerivative * Mathf.Max(0f, targetDistanceM - cursorDistanceM);
                    return heightM;
                }

                if (segmentStartM > cursorDistanceM)
                {
                    heightM += currentDerivative * (segmentStartM - cursorDistanceM);
                }

                heightM += segment.EvaluateHeightDeltaM(Mathf.Min(targetDistanceM, segmentEndM));

                if (targetDistanceM <= segmentEndM)
                {
                    return heightM;
                }

                cursorDistanceM = segmentEndM;
                currentDerivative = segment.EvaluateDerivative(segmentEndM);
            }

            heightM += currentDerivative * Mathf.Max(0f, targetDistanceM - cursorDistanceM);
            return heightM;
        }

        public static float GetGradientPermilleAt(List<TrackGeometryVerticalSegment> segments, float distanceOnGeometryM)
        {
            return GetDerivativeAt(segments, distanceOnGeometryM) * PermilleScale;
        }

        // 区間の隙間と末尾への勾配延長を含め、高さの一次微分を返す。
        public static float GetDerivativeAt(List<TrackGeometryVerticalSegment> segments, float distanceOnGeometryM)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            TrackGeometryVerticalSegment lastPassedSegment = null;
            for (int i = 0; i < segments.Count; i++)
            {
                TrackGeometryVerticalSegment segment = segments[i];
                if (segment == null || segment.lengthM <= MinSegmentLengthM)
                {
                    continue;
                }

                float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
                float segmentEndM = segmentStartM + Mathf.Max(0f, segment.lengthM);
                if (distanceOnGeometryM < segmentStartM)
                {
                    break;
                }

                if (distanceOnGeometryM <= segmentEndM)
                {
                    return segment.EvaluateDerivative(distanceOnGeometryM);
                }

                lastPassedSegment = segment;
            }

            if (lastPassedSegment == null)
            {
                return 0f;
            }

            float lastEndM = Mathf.Max(0f, lastPassedSegment.startDistanceM) + lastPassedSegment.lengthM;
            return lastPassedSegment.EvaluateDerivative(lastEndM);
        }
    }
}
