using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using System;

namespace Nakatetsu.Track.TrainGeometry
{
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrackCurveType")]
    public enum TrainGeometryCurveType
    {
        Straight,
        Curve,
        TransitionIn,  // 直線から円曲線へつなぐ緩和曲線です。
        TransitionOut  // 円曲線から直線へ戻す緩和曲線です。
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrackHorizontalSegment")]
    public class TrainGeometryHorizontalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public TrainGeometryCurveType trackCurveType;
        public float radiusM = 500f;
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.GuideLine", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrackVerticalSegment")]
    public class TrainGeometryVerticalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public float startGradientPermille;
        public float endGradientPermille;
    }
}
