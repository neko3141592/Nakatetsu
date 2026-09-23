using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public sealed class TrackGeometryConstantGradientSegment : TrackGeometryVerticalSegment
    {
        public float gradientPermille;

        public override float EvaluateHeightDeltaM(float distanceOnGeometryM)
        {
            if (lengthM <= MinSegmentLengthM)
            {
                return 0f;
            }

            float localDistanceM = Mathf.Clamp(GetLocalDistanceM(distanceOnGeometryM), 0f, lengthM);
            return gradientPermille * localDistanceM / PermilleScale;
        }

        public override float EvaluateSecondDerivative(float distanceOnGeometryM)
        {
            return 0f;
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            return lengthM <= MinSegmentLengthM ? 0f : gradientPermille / PermilleScale;
        }
    }
}
