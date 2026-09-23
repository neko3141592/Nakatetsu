using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public sealed class TrackGeometryCircularSegment : TrackGeometryHorizontalSegment
    {
        public float radiusM = 500f;

        public override void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees)
        {
            float localDistanceM = GetLocalDistanceM(distanceOnGeometryM);
            if (Mathf.Abs(radiusM) < 0.001f)
            {
                position = new Vector3(0f, 0f, localDistanceM);
                headingDegrees = 0f;
                return;
            }

            float theta = localDistanceM / radiusM;
            float x = radiusM * (1f - Mathf.Cos(theta));
            float z = radiusM * Mathf.Sin(theta);
            position = new Vector3(x, 0f, z);
            headingDegrees = theta * Mathf.Rad2Deg;
        }

        public override Vector3 EvaluateSecondDerivative(float distanceOnGeometryM)
        {
            if (Mathf.Abs(radiusM) < 0.001f)
            {
                return Vector3.zero;
            }

            float theta = GetLocalDistanceM(distanceOnGeometryM) / radiusM;
            return new Vector3(Mathf.Cos(theta) / radiusM, 0f, -Mathf.Sin(theta) / radiusM);
        }

        public override Vector3 EvaluateDerivative(float distanceOnGeometryM)
        {
            if (Mathf.Abs(radiusM) < 0.001f)
            {
                return new Vector3(0f, 0f, 1f);
            }

            float localDistanceM = GetLocalDistanceM(distanceOnGeometryM);
            float dxDl = Mathf.Sin(localDistanceM / radiusM);
            float dzDl = Mathf.Cos(localDistanceM / radiusM);

            return new Vector3(dxDl, 0f, dzDl);
        }
    }
}
