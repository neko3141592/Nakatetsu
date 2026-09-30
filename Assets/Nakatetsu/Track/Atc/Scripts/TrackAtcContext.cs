using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Atc
{
    public sealed class TrackAtcContext
    {
        // コンパイル済み定義を参照する。実行時には書き換えない。
        public TrackAtcGraphDefinition Graph { get; internal set; }
        public TrackAtcInput Input { get; } = new();
        public TrackAtcOutput Output { get; } = new();

        public TrackAtcState State { get; } = new();
    }

    public sealed class TrackAtcInput
    {
        public readonly Dictionary<string, bool> OccupiedByCircuitId = new();

        // 設定中の進路のみ。キーはATC GraphのinterlockingRouteIdに対応する。
        public readonly Dictionary<string, TrackAtcRouteInput> RoutesById = new();

        // 取得できた転轍機のみ。未登録を所定位置とみなさない。
        public readonly Dictionary<string, TrackConnectionState> TurnoutsById = new();
    }

    public sealed class TrackAtcRouteInput
    {
        public bool ProceedAllowed;
        public bool CancelPending;
        public bool RouteLocked;
        public readonly Dictionary<string, TrackSwitchPosition> RequiredTurnoutsById = new();
    }

    public sealed class TrackAtcOutput
    {
        // キーは現在Edge ID、値は次Edge ID。nullは継続なし、未登録は未計算。
        public readonly Dictionary<string, string> NextEdgeById = new();
    }

    public sealed class TrackAtcState
    {
        public HashSet<string> visitedAtcEdgeIds = new();
    }
}
