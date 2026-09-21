using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    /// <summary>Evaluates the running reference line; has no graph or scene dependencies.</summary>
    public static class GuideLineCalculator
    {
        public static bool TryEvaluate(GuideLineDefinition definition, float distanceM, out GuideLineSample sample)
        {
            sample = default;
            if (definition == null || !IsFinite(distanceM) ||
                !IsFinite(definition.lengthM) || definition.lengthM <= 0f ||
                definition.horizontalSegments == null || definition.horizontalSegments.Count == 0)
                return false;

            float distance = Mathf.Clamp(distanceM, 0f, definition.lengthM);
            if (!TryResolveNativeGeometryPose(definition, distance, out Vector3 position,
                out Vector3 tangent, out Quaternion rotation)) return false;
            float gradient = GuideLineProfileCalculator.GetGradientPermilleAt(definition.verticalSegments, distance);
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(tangent.x) || !IsFinite(tangent.y) || !IsFinite(tangent.z) ||
                !IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w) ||
                !IsFinite(gradient)) return false;

            sample = new GuideLineSample(distance, position, tangent, rotation, gradient);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static void CalculateStraight(float lengthM, out float x, out float z, out float angleDegree)
        {
            x = 0f;
            z = lengthM;
            angleDegree = 0f;
        }

        public static void CalculateCircularCurve(float lengthM, float radiusM, out float x, out float z, out float angleDegree)
        {
            if (Mathf.Abs(radiusM) < 0.001f)
            {
                CalculateStraight(lengthM, out x, out z, out angleDegree);
                return;
            }

            float theta = lengthM / radiusM;
            x = radiusM * (1f - Mathf.Cos(theta));
            z = radiusM * Mathf.Sin(theta);
            angleDegree = theta * Mathf.Rad2Deg;
        }

        public static void CalculateCubicTransitionIn(
            float lengthM,
            float totalLengthM,
            float radiusM,
            out float x,
            out float z,
            out float angleDegree)
        {
            if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
            {
                CalculateStraight(lengthM, out x, out z, out angleDegree);
                return;
            }

            float theta = (lengthM * lengthM) / (2f * totalLengthM * radiusM);
            x = (lengthM * lengthM * lengthM) / (6f * totalLengthM * radiusM);
            z = lengthM;
            angleDegree = theta * Mathf.Rad2Deg;
        }

        public static void CalculateCubicTransitionOut(
            float lengthM,
            float totalLengthM,
            float radiusM,
            out float x,
            out float z,
            out float angleDegree)
        {
            if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
            {
                CalculateStraight(lengthM, out x, out z, out angleDegree);
                return;
            }

            float theta = lengthM / radiusM - (lengthM * lengthM) / (2f * radiusM * totalLengthM);
            x = (lengthM * lengthM) / (2f * radiusM) - (lengthM * lengthM * lengthM) / (6f * radiusM * totalLengthM);
            z = lengthM;
            angleDegree = theta * Mathf.Rad2Deg;
        }

        private static bool TryResolveNativeGeometryPose(
            GuideLineDefinition geometry,
            float distanceM,
            out Vector3 position,
            out Vector3 tangent,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            tangent = Vector3.forward;
            rotation = Quaternion.identity;

            Vector3 currentPos = geometry.originPosition;
            Quaternion currentRot = GetPlanRotation(geometry.originRotation);

            if (!TryResolveHorizontalPosition(geometry.horizontalSegments, distanceM, ref currentPos, ref currentRot))
            {
                return false;
            }

            float heightM = GuideLineProfileCalculator.GetVerticalHeightAt(geometry.verticalSegments, distanceM);
            float currentPermille = GuideLineProfileCalculator.GetGradientPermilleAt(geometry.verticalSegments, distanceM);

            position = currentPos;
            position.y = geometry.originPosition.y + heightM;

            float pitchDegree = -Mathf.Atan(currentPermille / 1000f) * Mathf.Rad2Deg;
            rotation = currentRot * Quaternion.Euler(pitchDegree, 0f, 0f);
            tangent = rotation * Vector3.forward;
            return true;
        }

        private static bool TryResolveHorizontalPosition(
            List<TrackHorizontalSegment> segments,
            float distanceM,
            ref Vector3 currentPos,
            ref Quaternion currentRot)
        {
            if (segments == null || segments.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                TrackHorizontalSegment segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
                float segmentLengthM = Mathf.Max(0f, segment.lengthM);
                float segmentEndM = segmentStartM + segmentLengthM;

                if (distanceM <= segmentStartM)
                {
                    break;
                }

                float localDistanceM = Mathf.Min(distanceM, segmentEndM) - segmentStartM;
                if (localDistanceM <= 0f)
                {
                    continue;
                }

                CalculateHorizontal(
                    segment.trackCurveType,
                    localDistanceM,
                    segmentLengthM,
                    segment.radiusM,
                    out float localX,
                    out float localZ,
                    out float angleDegree
                );
                currentPos += currentRot * new Vector3(localX, 0f, localZ);
                currentRot *= Quaternion.Euler(0f, angleDegree, 0f);

                if (distanceM <= segmentEndM)
                {
                    return true;
                }
            }

            return true;
        }

        internal static void CalculateHorizontal(
            TrackCurveType type,
            float localDistanceM,
            float segmentLengthM,
            float radiusM,
            out float localX,
            out float localZ,
            out float angleDegree)
        {
            switch (type)
            {
                case TrackCurveType.Curve:
                    CalculateCircularCurve(localDistanceM, radiusM, out localX, out localZ, out angleDegree);
                    break;
                case TrackCurveType.TransitionIn:
                    CalculateCubicTransitionIn(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                    break;
                case TrackCurveType.TransitionOut:
                    CalculateCubicTransitionOut(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                    break;
                default:
                    CalculateStraight(localDistanceM, out localX, out localZ, out angleDegree);
                    break;
            }
        }

        internal static Quaternion GetPlanRotation(Quaternion worldRotation)
        {
            Vector3 forwardXZ = worldRotation * Vector3.forward;
            forwardXZ.y = 0f;
            return forwardXZ.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forwardXZ.normalized) : Quaternion.identity;
        }
    }
}
