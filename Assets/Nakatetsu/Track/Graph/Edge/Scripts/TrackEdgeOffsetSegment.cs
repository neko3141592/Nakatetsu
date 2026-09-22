using System;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    public abstract class TrackEdgeOffsetSegment
    {
        public float startDistanceOnGeometryM;
        public float endDistanceOnGeometryM;

        /// <summary>
        /// Lateral offset in metres, positive to the right in the Geometry's increasing-distance direction.
        /// The caller validates the definition and selects a segment containing the requested distance.
        /// </summary>
        public abstract float EvaluateOffsetM(float distanceOnGeometryM);

        /// <summary>
        /// Derivative of offset with respect to Geometry distance (metres per metre).
        /// Independent of Edge orientation and train movement direction.
        /// </summary>
        public abstract float EvaluateDerivative(float distanceOnGeometryM);
    }
}
