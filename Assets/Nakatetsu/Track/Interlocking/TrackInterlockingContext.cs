using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking
{
    public enum TrackInterlockingCircuitPassageState
    {
        NotEntered,
        Occupied,
        Passed
    }

    public enum OverrunProtectionPhase
    {
        None,
        Setting,
        Established,
        ReleaseTiming,
        Released,
    }

    public sealed class TrackInterlockingRouteState
    {
        // 進行許可
        public bool ProceedAllowed { get; internal set; }

        // 転轍機の実位置と、保持中の過走防護を照査した開通状態
        public bool PathEstablished { get; internal set; }

        // キャンセル済みかどうか
        public bool CancelPending { get; internal set; }

        //　接近鎖錠
        public bool ApproachLocked { get; internal set; }
        public float ApproachReleaseRemainingSeconds { get; internal set; }

        // 進路鎖錠
        public bool RouteLocked { get; internal set; }

        // 通過状態
        internal readonly Dictionary<string, TrackInterlockingCircuitPassageState> CircuitPassageById = new();


        // 過走防護処理
        public OverrunProtectionMode overrunProtectionMode;
        public OverrunProtectionPhase overrunProtectionPhase;
        public float OverrunProtectionReleaseRemainingSeconds;
        public bool OverrunArrivalDetected { get; internal set; }
        public bool OverrunAutomaticReleaseBlocked { get; internal set; }

    }

    public sealed class TrackInterlockingContext
    {
        internal readonly Dictionary<string, InterlockingRoute> RoutesById = new();
        internal readonly Dictionary<string, TurnoutTrackCircuitLock> TurnoutTrackCircuitLocksByConnectionId = new();
        // 未登録の進路は、現在設定されていない。
        internal readonly Dictionary<string, TrackInterlockingRouteState> RouteStatesById = new();

        public bool IsInitialized { get; internal set; }
    }
}
