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
        public bool ProceedAllowed { get; internal set; }
        public bool CancelPending { get; internal set; }
        public bool ApproachLocked { get; internal set; }
        public float ApproachReleaseRemainingSeconds { get; internal set; }
        public bool RouteLocked { get; internal set; }

        internal readonly Dictionary<string, TrackInterlockingCircuitPassageState> CircuitPassageById = new();
    }

    public sealed class TrackInterlockingContext
    {
        internal readonly Dictionary<string, InterlockingRoute> RoutesById = new();
        // 未登録の進路は、現在設定されていない。
        internal readonly Dictionary<string, TrackInterlockingRouteState> RouteStatesById = new();

        public bool IsInitialized { get; internal set; }
    }
}
