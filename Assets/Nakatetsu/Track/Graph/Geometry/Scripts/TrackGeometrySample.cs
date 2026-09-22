using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    public readonly struct TrackGeometrySample
    {
        public float DistanceM { get; }
        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Quaternion Rotation { get; }
        public float GradientPermille { get; }

        public TrackGeometrySample(float distanceM, Vector3 position, Vector3 tangent,
            Quaternion rotation, float gradientPermille)
        {
            DistanceM = distanceM;
            Position = position;
            Tangent = tangent;
            Rotation = rotation;
            GradientPermille = gradientPermille;
        }
    }
}
