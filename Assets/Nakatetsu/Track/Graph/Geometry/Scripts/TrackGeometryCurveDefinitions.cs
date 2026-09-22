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
    // Read-only migration shape for the former inline, enum-discriminated format.
    internal sealed class LegacyTrackGeometryHorizontalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public TrackGeometryCurveType trackCurveType;
        public float radiusM = 500f;
    }

    [Serializable]
    // Read-only migration shape for the former inline vertical segments.
    internal sealed class LegacyTrackGeometryVerticalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;
        public float startGradientPermille;
        public float endGradientPermille;
    }
}
