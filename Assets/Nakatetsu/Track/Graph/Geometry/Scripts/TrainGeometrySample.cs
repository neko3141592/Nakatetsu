using UnityEngine;

namespace Nakatetsu.Track.TrainGeometry
{
    public readonly struct TrainGeometrySample
    {
        public float DistanceM { get; }
        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Quaternion Rotation { get; }
        public float GradientPermille { get; }

        public TrainGeometrySample(float distanceM, Vector3 position, Vector3 tangent,
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
