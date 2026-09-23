using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public abstract class TrackEdgeOffsetSegment
    {
        public float startDistanceOnGeometryM;
        public float endDistanceOnGeometryM;

        // Geometry距離が増える向きの右側を正とした横オフセット[m]。
        public abstract float EvaluateOffsetM(float distanceOnGeometryM);

        // Geometry距離に対する横オフセットの微分。Edgeや列車の向きには依存しない。
        public abstract float EvaluateDerivative(float distanceOnGeometryM);
    }
}
