#nullable enable

using MessagePack;

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>1進路の構成状態と過走防護情報。</summary>
    [MessagePackObject]
    public sealed class InterlockingRouteStatus
    {
        // 進路に必要な設備情報の取得可否。falseも取得できた進路状態として保持する。
        [Key(0)]
        public bool IsAvailable { get; set; }

        [Key(1)]
        public bool IsRouteSet { get; set; }

        [Key(2)]
        public bool PathEstablished { get; set; }

        [Key(3)]
        public bool ProceedAllowed { get; set; }

        [Key(4)]
        public bool RouteLocked { get; set; }

        [Key(5)]
        public bool CancelPending { get; set; }

        [Key(6)]
        public bool ApproachLocked { get; set; }

        [Key(7)]
        public float ApproachReleaseRemainingSeconds { get; set; }

        [Key(8)]
        public OverrunProtectionMode OverrunMode { get; set; }

        [Key(9)]
        public OverrunProtectionPhase OverrunPhase { get; set; }

        [Key(10)]
        public float OverrunReleaseRemainingSeconds { get; set; }
    }
}
