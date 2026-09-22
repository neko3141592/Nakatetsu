using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public sealed class TrackEdgeConstantOffsetSegment : TrackEdgeOffsetSegment
    {
        public float offsetM;

        public override float EvaluateOffsetM(float distanceOnGeometryM)
        {
            return offsetM;
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            return 0f;
        }
    }
}
