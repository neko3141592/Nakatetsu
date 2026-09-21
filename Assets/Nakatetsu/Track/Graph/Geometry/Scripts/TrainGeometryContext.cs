using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.TrainGeometry
{
    public sealed class TrainGeometryContext
    {
        public TrainGeometryWorkspace Workspace { get; } = new TrainGeometryWorkspace();
    }

    public sealed class TrainGeometryWorkspace
    {
        public readonly List<TrainGeometryHorizontalStart> horizontalStarts = new();
        public readonly List<TrainGeometryVerticalStart> verticalStarts = new();
    }

    public struct TrainGeometryHorizontalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Horizontal pose: height and pitch are evaluated separately.
        public Vector3 position;
        public Quaternion rotation;
    }

    public struct TrainGeometryVerticalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Height relative to the TrainGeometry origin.
        public float heightM;
    }
}
