using System;

namespace Nakatetsu.Track.Graph.Edge
{
    // 始終点の横オフセットを3次式で補間する。両端の相対的な傾きは0。
    [Serializable]
    public sealed class TrackEdgeCubicOffsetSegment : TrackEdgeOffsetSegment
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

            float t = (distanceOnGeometryM - startDistanceOnGeometryM) / lengthM;
            return startOffsetM + (endOffsetM - startOffsetM) * t * t * (3f - 2f * t);
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            float lengthM = endDistanceOnGeometryM - startDistanceOnGeometryM;
            if (lengthM <= 0f)
            {
                return float.NaN;
            }

            float t = (distanceOnGeometryM - startDistanceOnGeometryM) / lengthM;
            return (endOffsetM - startOffsetM) / lengthM * 6f * t * (1f - t);
        }
    }
}
