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
        // 高さとピッチは水平姿勢とは別に評価する。
        public Vector3 position;
        public Quaternion rotation;
    }

    public struct TrackGeometryVerticalStart
    {
        public int segmentIndex;
        public float startDistanceM;
        public float endDistanceM;
        // TrackGeometry起点からの相対高さ。
        public float heightM;
    }
}
