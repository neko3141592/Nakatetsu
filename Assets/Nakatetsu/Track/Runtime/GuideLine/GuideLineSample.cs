using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    public readonly struct GuideLineSample
    {
        public float DistanceM { get; }
        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Quaternion Rotation { get; }
        public float GradientPermille { get; }
        public float CantMm { get; }

        public GuideLineSample(float distanceM, Vector3 position, Vector3 tangent,
            Quaternion rotation, float gradientPermille, float cantMm)
        {
            DistanceM = distanceM;
            Position = position;
            Tangent = tangent;
            Rotation = rotation;
            GradientPermille = gradientPermille;
            CantMm = cantMm;
        }
    }
}
