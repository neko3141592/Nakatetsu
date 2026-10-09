using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    // 編集・保存する生成元。生成済みATC EdgeのIDには依存しない。
    [Serializable]
    public sealed class TrackAtcGraphCompileDefinition
    {
        public string atcGraphId;
        // 線区全体の営業最高速度[km/h]。
        public float maximumOperatingSpeedKmh;
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
        // 進行順。分岐側は連動進路の転轍機条件と線路の接続情報から解決する。
        public List<string> trackCircuitIds = new();

        // 軌道回路が1つだけの場合の進入方向。複数の場合は回路の並びから求める。
        public TrackAtcTravelDirection entryDirection;
    }
}
