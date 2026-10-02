using System.Collections.Generic;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
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
        public TrackAtcWorkspace Workspace { get; } = new();
    }

    public sealed class TrackAtcInput
    {
        // 車上と同じシミュレーション時刻。未取得のまま有効な電文を作らない。
        public double simulationTimeSeconds = double.NaN;

        public readonly Dictionary<string, bool> OccupiedByCircuitId = new();

        // 設定中の進路のみ。キーはATC GraphのinterlockingRouteIdに対応する。
        public readonly Dictionary<string, TrackAtcRouteInput> RoutesById = new();

        // 取得できた転轍機のみ。未登録を所定位置とみなさない。
        public readonly Dictionary<string, TrackConnectionState> TurnoutsById = new();
    }

    public sealed class TrackAtcRouteInput
    {
        public bool ProceedAllowed;
        // 新規進入の許可とは別に、進入後も確保されている経路を表す。
        public bool PathEstablished;
        public OverrunProtectionMode OverrunMode;
        public bool CancelPending;
        public bool RouteLocked;
        public readonly Dictionary<string, TrackSwitchPosition> RequiredTurnoutsById = new();
    }

    public sealed class TrackAtcOutput
    {
        // 軌道回路ID -> telegram。更新開始時にクリアし、同じ更新内の結果を集約する。
        public Dictionary<string, TrackCircuitAtcTelegram> telegrams = new();
    }

    public sealed class TrackAtcState
    {
        public readonly Dictionary<string, string> NextEdgeById = new();
    }

    public sealed class TrackAtcWorkspace
    {
        public readonly Dictionary<string, TrackAtcGraphEdge> atcEdgesById = new();
        public readonly Dictionary<string, TrackAtcGraphNode> atcNodesById = new();
        // キーはATC進路ID。Input.RoutesByIdの連動進路IDとは異なる。
        public readonly Dictionary<string, TrackAtcRouteDefinition> atcRoutesById = new();
        public readonly HashSet<string> visitedAtcEdgeIds = new();
    }
}
