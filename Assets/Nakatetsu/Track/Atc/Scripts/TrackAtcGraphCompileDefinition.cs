using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Atc
{
    // 編集・保存する生成元。生成済みATC EdgeのIDには依存しない。
    [Serializable]
    public sealed class TrackAtcGraphCompileDefinition
    {
        public string atcGraphId;
        public float gradientSampleIntervalM = 10f;
        public List<TrackAtcCircuitSetting> circuits = new();
        public List<TrackAtcSpeedLimitSourceSection> speedLimits = new();
        public List<TrackAtcRouteSourceDefinition> routes = new();
    }

    [Serializable]
    public sealed class TrackAtcCircuitSetting
    {
        public string trackCircuitId;
        public TrackAtcEdgeControlKind controlKind;
        // 回路全体の基本制限。区間制限と重なる場合は低い方を採用する。
        public float speedLimitKmh;
    }

    [Serializable]
    public sealed class TrackAtcSpeedLimitSourceSection
    {
        public string trackEdgeId;
        public float startDistanceOnGeometryM;
        public float endDistanceOnGeometryM;
        public float speedLimitKmh;
    }

    [Serializable]
    public sealed class TrackAtcRouteSourceDefinition
    {
        public string atcRouteId;
        public string interlockingRouteId;
        // 進行順。軌道回路IDだけでは分岐のどの枝を通るか決まらないため、物理区間を指定する。
        public List<TrackAtcRouteSourceSpan> path = new();
    }

    [Serializable]
    public sealed class TrackAtcRouteSourceSpan
    {
        public string trackEdgeId;
        // 入口→出口。Geometryの増加方向と逆向きでも指定できる。
        public float entryDistanceOnGeometryM;
        public float exitDistanceOnGeometryM;
    }
}
