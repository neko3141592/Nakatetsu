using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    // GraphやSceneに依存せず、走行用の基準線を評価する。
    public static class TrackGeometryCalculator
    {
        public static bool TryEvaluate(TrackGeometryDefinition definition, float distanceM, out TrackSample sample)
        {
            sample = default;
            if (definition == null || !IsFinite(distanceM) ||
                !IsFinite(definition.lengthM) || definition.lengthM <= 0f ||
                definition.horizontalSegments == null || definition.horizontalSegments.Count == 0)
            {
                return false;
            }

            float distance = Mathf.Clamp(distanceM, 0f, definition.lengthM);
            if (!TryResolveNativeGeometryPose(definition, distance, out Vector3 position,
                out Vector3 tangent, out Quaternion rotation))
            {
                return false;
            }

            float gradient = TrackGeometryProfileCalculator.GetGradientPermilleAt(definition.verticalSegments, distance);
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(tangent.x) || !IsFinite(tangent.y) || !IsFinite(tangent.z) ||
                !IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w) ||
                !IsFinite(gradient))
            {
                return false;
            }

            sample = new TrackSample(distance, position, tangent, rotation, gradient);
            return true;
        }

        // 従来の姿勢評価を変えずに、位置と正規化しない一次微分を求める。
        public static bool TryEvaluateAtGeometryDistance(TrackGeometryDefinition definition, float distanceOnGeometryM,
            out Vector3 position, out Vector3 derivative)
        {
            return TryEvaluateDerivatives(definition, distanceOnGeometryM, false, out position, out derivative, out _);
        }

        // ワールド空間での正規化しない二階微分も返す。
        public static bool TryEvaluateAtGeometryDistance(TrackGeometryDefinition definition, float distanceOnGeometryM,
            out Vector3 position, out Vector3 derivative, out Vector3 secondDerivative)
        {
            return TryEvaluateDerivatives(definition, distanceOnGeometryM, true, out position, out derivative, out secondDerivative);
        }

        private static bool TryEvaluateDerivatives(TrackGeometryDefinition definition, float distanceOnGeometryM,
            bool includeSecondDerivative, out Vector3 position, out Vector3 derivative, out Vector3 secondDerivative)
        {
            position = default;
            derivative = default;
            secondDerivative = default;

            if (definition == null || !IsFinite(distanceOnGeometryM) || !IsFinite(definition.lengthM)
                || definition.lengthM <= 0f || distanceOnGeometryM < 0f || distanceOnGeometryM > definition.lengthM
                || definition.horizontalSegments == null || definition.horizontalSegments.Count == 0
                || !IsFinite(definition.originPosition))
            {
                return false;
            }

            var originRotation = definition.originRotation;
            if (!IsFinite(originRotation.x) || !IsFinite(originRotation.y)
                || !IsFinite(originRotation.z) || !IsFinite(originRotation.w))
            {
                return false;
            }

            // 高さと区間の隙間を評価する前に、縦断区間の範囲を確認する。
            float previousVerticalEndM = 0f;
            if (definition.verticalSegments != null)
            {
                foreach (var segment in definition.verticalSegments)
                {
                    if (segment == null || !IsFinite(segment.startDistanceM) || !IsFinite(segment.lengthM))
                    {
                        return false;
                    }

                    if (segment.lengthM <= 0.001f)
                    {
                        continue;
                    }

                    float endM = segment.startDistanceM + segment.lengthM;
                    if (segment.startDistanceM < previousVerticalEndM || !IsFinite(endM))
                    {
                        return false;
                    }

                    previousVerticalEndM = endM;
                }
            }

            Vector3 currentPosition = definition.originPosition;
            Quaternion currentRotation = GetPlanRotation(originRotation);
            float previousEndM = 0f;
            foreach (var segment in definition.horizontalSegments)
            {
                if (segment == null || !IsFinite(segment.startDistanceM) || !IsFinite(segment.lengthM)
                    || segment.lengthM <= 0f || segment.startDistanceM != previousEndM)
                {
                    return false;
                }

                float endM = segment.startDistanceM + segment.lengthM;
                if (!IsFinite(endM))
                {
                    return false;
                }

                float sampleDistanceM = Mathf.Min(distanceOnGeometryM, endM);
                segment.EvaluatePosition(sampleDistanceM, out Vector3 localPosition, out float headingDegrees);
                if (!IsFinite(localPosition) || !IsFinite(headingDegrees))
                {
                    return false;
                }

                currentPosition += currentRotation * localPosition;

                if (distanceOnGeometryM <= endM)
                {
                    Vector3 localDerivative = segment.EvaluateDerivative(distanceOnGeometryM);
                    if (!IsFinite(localDerivative))
                    {
                        return false;
                    }

                    Vector3 worldDerivative = currentRotation * localDerivative;
                    currentPosition.y = definition.originPosition.y
                        + TrackGeometryProfileCalculator.GetVerticalHeightAt(definition.verticalSegments, distanceOnGeometryM);
                    worldDerivative.y = TrackGeometryProfileCalculator.GetDerivativeAt(definition.verticalSegments, distanceOnGeometryM);
                    if (!IsFinite(currentPosition) || !IsFinite(worldDerivative))
                    {
                        return false;
                    }

                    Vector3 worldSecondDerivative = Vector3.zero;
                    if (includeSecondDerivative)
                    {
                        Vector3 localSecondDerivative = segment.EvaluateSecondDerivative(distanceOnGeometryM);
                        if (!IsFinite(localSecondDerivative))
                        {
                            return false;
                        }

                        worldSecondDerivative = currentRotation * localSecondDerivative;
                        worldSecondDerivative.y = TrackGeometryProfileCalculator.GetSecondDerivativeAt(
                            definition.verticalSegments, distanceOnGeometryM);
                        if (!IsFinite(worldSecondDerivative))
                        {
                            return false;
                        }
                    }

                    position = currentPosition;
                    derivative = worldDerivative;
                    secondDerivative = worldSecondDerivative;
                    return true;
                }

                currentRotation *= Quaternion.Euler(0f, headingDegrees, 0f);
                previousEndM = endM;
            }

            return false;
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool TryResolveNativeGeometryPose(
            TrackGeometryDefinition geometry,
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

            float heightM = TrackGeometryProfileCalculator.GetVerticalHeightAt(geometry.verticalSegments, distanceM);
            float currentPermille = TrackGeometryProfileCalculator.GetGradientPermilleAt(geometry.verticalSegments, distanceM);

            position = currentPos;
            position.y = geometry.originPosition.y + heightM;

            float pitchDegree = -Mathf.Atan(currentPermille / 1000f) * Mathf.Rad2Deg;
            rotation = currentRot * Quaternion.Euler(pitchDegree, 0f, 0f);
            tangent = rotation * Vector3.forward;
            return true;
        }

        private static bool TryResolveHorizontalPosition(
            List<TrackGeometryHorizontalSegment> segments,
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
                TrackGeometryHorizontalSegment segment = segments[i];
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

                segment.EvaluatePosition(Mathf.Min(distanceM, segmentEndM),
                    out Vector3 localPosition, out float angleDegree);
                currentPos += currentRot * localPosition;
                currentRot *= Quaternion.Euler(0f, angleDegree, 0f);

                if (distanceM <= segmentEndM)
                {
                    return true;
                }
            }

            return true;
        }

        internal static Quaternion GetPlanRotation(Quaternion worldRotation)
        {
            Vector3 forwardXZ = worldRotation * Vector3.forward;
            forwardXZ.y = 0f;
            return forwardXZ.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forwardXZ.normalized) : Quaternion.identity;
        }
    }
}
