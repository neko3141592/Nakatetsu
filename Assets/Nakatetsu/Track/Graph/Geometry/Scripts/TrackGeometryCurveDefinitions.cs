using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using System;

namespace Nakatetsu.Track.Graph.Geometry
{
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryCurveType")]
    public enum TrackGeometryCurveType
    {
        Straight,
        Curve,
        TransitionIn,  // 直線から円曲線へつなぐ緩和曲線です。
        TransitionOut  // 円曲線から直線へ戻す緩和曲線です。
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryHorizontalSegment")]
    public class TrackGeometryHorizontalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public TrackGeometryCurveType trackCurveType;
        public float radiusM = 500f;
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrainGeometryVerticalSegment")]
    public class TrackGeometryVerticalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public float startGradientPermille;
        public float endGradientPermille;
    }
}
