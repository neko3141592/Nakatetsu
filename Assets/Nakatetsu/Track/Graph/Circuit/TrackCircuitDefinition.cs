using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Graph.Circuit
{
    public enum TrackControlKind
    {
        Unknown = 0,
        Block = 1,
        Interlocking = 2
    }

    [Serializable]
    public sealed class TrackCircuitDefinition
    {
        public string circuitId;

        // JSONから読み込む制御区分。現時点では動作判定には使用しない。
        public TrackControlKind controlKind;

        public List<TrackCircuitSection> sections = new();
    }

    [Serializable]
    public sealed class TrackCircuitSection
    {
        public string edgeId;
        public float startDistanceOnGeometryM;
        public float endDistanceOnGeometryM;
    } 
}
