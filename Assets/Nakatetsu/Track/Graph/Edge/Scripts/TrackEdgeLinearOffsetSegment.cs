using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public sealed class TrackEdgeLinearOffsetSegment : TrackEdgeOffsetSegment
    {
        public float startOffsetM;
        public float endOffsetM;

        public override float EvaluateOffsetM(float distanceOnGeometryM)
        {
            float lengthM = endDistanceOnGeometryM - startDistanceOnGeometryM;
            if (lengthM <= 0f)
            {
                return float.NaN;
            }

            return startOffsetM + (endOffsetM - startOffsetM)
                * ((distanceOnGeometryM - startDistanceOnGeometryM) / lengthM);
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            float lengthM = endDistanceOnGeometryM - startDistanceOnGeometryM;
            if (lengthM <= 0f)
            {
                return float.NaN;
            }

            return (endOffsetM - startOffsetM) / lengthM;
        }
    }
}
