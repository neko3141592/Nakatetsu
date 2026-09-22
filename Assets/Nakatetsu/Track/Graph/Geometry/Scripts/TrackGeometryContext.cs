using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    public sealed class TrackGeometryContext
    {
        public TrackGeometryWorkspace Workspace { get; } = new TrackGeometryWorkspace();
    }

    public sealed class TrackGeometryWorkspace
    {
        public readonly List<TrackGeometryHorizontalStart> horizontalStarts = new();
        public readonly List<TrackGeometryVerticalStart> verticalStarts = new();
    }

    public struct TrackGeometryHorizontalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Horizontal pose: height and pitch are evaluated separately.
        public Vector3 position;
        public Quaternion rotation;
    }

    public struct TrackGeometryVerticalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // Height relative to the TrackGeometry origin.
        public float heightM;
    }
}
