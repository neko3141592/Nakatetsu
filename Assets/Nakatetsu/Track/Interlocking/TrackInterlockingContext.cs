using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public enum TrackInterlockingCircuitPassageState
    {
        NotEntered,
        Occupied,
        Passed
    }

    public sealed class TrackInterlockingRouteState
    {
        // 進行許可
        public bool ProceedAllowed { get; internal set; }

        // キャンセル済みかどうか
        public bool CancelPending { get; internal set; }

        //　接近鎖錠
        public bool ApproachLocked { get; internal set; }
        public float ApproachReleaseRemainingSeconds { get; internal set; }

        // 進路鎖錠
        public bool RouteLocked { get; internal set; }

        // 通過状態

        internal readonly Dictionary<string, TrackInterlockingCircuitPassageState> CircuitPassageById = new();

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
