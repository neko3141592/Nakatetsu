using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public sealed class TrackGeometryStraightSegment : TrackGeometryHorizontalSegment
    {
        public override void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees)
        {
            position = new Vector3(0f, 0f, GetLocalDistanceM(distanceOnGeometryM));
            headingDegrees = 0f;
        }

        public override Vector3 EvaluateSecondDerivative(float distanceOnGeometryM)
        {
            return Vector3.zero;
        }

        public override Vector3 EvaluateDerivative(float distanceOnGeometryM)
        {
            return new Vector3(0f, 0f, 1f);
        }
    }
}
