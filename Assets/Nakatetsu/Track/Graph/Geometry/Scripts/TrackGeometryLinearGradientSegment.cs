using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public sealed class TrackGeometryLinearGradientSegment : TrackGeometryVerticalSegment
    {
        public float startGradientPermille;
        public float endGradientPermille;

        public override float EvaluateHeightDeltaM(float distanceOnGeometryM)
        {
            if (lengthM <= MinSegmentLengthM) return 0f;
            float localDistanceM = Mathf.Clamp(GetLocalDistanceM(distanceOnGeometryM), 0f, lengthM);
            return (startGradientPermille * localDistanceM
                + (endGradientPermille - startGradientPermille) * localDistanceM * localDistanceM / (2f * lengthM)) / PermilleScale;
        }

        public override float EvaluateSecondDerivative(float distanceOnGeometryM)
        {
            if (lengthM <= MinSegmentLengthM) return 0f;
            return (endGradientPermille - startGradientPermille) / lengthM / PermilleScale;
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            if (lengthM <= MinSegmentLengthM) return 0f;
            float t = Mathf.Clamp01(GetLocalDistanceM(distanceOnGeometryM) / lengthM);
            return Mathf.Lerp(startGradientPermille, endGradientPermille, t) / PermilleScale;
        }
    }
}
