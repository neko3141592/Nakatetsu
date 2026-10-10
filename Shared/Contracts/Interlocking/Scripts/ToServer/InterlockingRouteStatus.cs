#nullable enable

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>1進路の構成状態と過走防護情報。</summary>
    public sealed class InterlockingRouteStatus
    {
        // 進路に必要な設備情報の取得可否。falseも取得できた進路状態として保持する。
        public bool IsAvailable { get; set; }
        public bool IsRouteSet { get; set; }
        public bool PathEstablished { get; set; }
        public bool ProceedAllowed { get; set; }
        public bool RouteLocked { get; set; }
        public bool CancelPending { get; set; }
        public bool ApproachLocked { get; set; }
        public float ApproachReleaseRemainingSeconds { get; set; }
        public OverrunProtectionMode OverrunMode { get; set; }
        public OverrunProtectionPhase OverrunPhase { get; set; }
        public float OverrunReleaseRemainingSeconds { get; set; }
    }
}
