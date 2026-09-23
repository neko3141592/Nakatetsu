using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public sealed class TrackGeometryTransitionOutSegment : TrackGeometryHorizontalSegment
    {
        public float radiusM = 500f;

        public override void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees)
        {
            float localDistanceM = GetLocalDistanceM(distanceOnGeometryM);
            float totalLengthM = Mathf.Max(0f, lengthM);
            if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
            {
                position = new Vector3(0f, 0f, localDistanceM);
                headingDegrees = 0f;
                return;
            }

            float theta = localDistanceM / radiusM - (localDistanceM * localDistanceM) / (2f * radiusM * totalLengthM);
            float x = (localDistanceM * localDistanceM) / (2f * radiusM) - (localDistanceM * localDistanceM * localDistanceM) / (6f * radiusM * totalLengthM);
            position = new Vector3(x, 0f, localDistanceM);
            headingDegrees = theta * Mathf.Rad2Deg;
        }

        public override Vector3 EvaluateSecondDerivative(float distanceOnGeometryM)
        {
            float totalLengthM = Mathf.Max(0f, lengthM);
            if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f) return Vector3.zero;
            float localDistanceM = GetLocalDistanceM(distanceOnGeometryM);
            return new Vector3((1f - localDistanceM / totalLengthM) / radiusM, 0f, 0f);
        }

        public override Vector3 EvaluateDerivative(float distanceOnGeometryM)
        {
            float localDistanceM = GetLocalDistanceM(distanceOnGeometryM);
            float totalLengthM = Mathf.Max(0f, lengthM);
            if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
            {
                return new Vector3(0f, 0f, 1f);
            }

            float dxDl = localDistanceM / radiusM - (localDistanceM * localDistanceM) / (2f * radiusM * totalLengthM);
            float dzDl = 1f;

            return new Vector3(dxDl, 0f, dzDl);
        }
    }
}
