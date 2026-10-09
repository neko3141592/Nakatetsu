using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking.Management
{
    public sealed class TrackInterlockingManagementInput
    {
        // 駅IDをキーとする。更新順は登録側で管理し、この辞書では定義しない。
        public readonly Dictionary<string, TrackInterlockingManagementStationInput> StationsById = new();
    }

    public sealed class TrackInterlockingManagementStationInput
    {
        public bool IsInitialized;
        public bool HasOutput;

        // 初期化済みでOutputを取得できた駅の、公開進路状態を値として取り込む。
        public readonly Dictionary<string, TrackInterlockingManagementRouteInput> RoutesById = new();
    }

    public sealed class TrackInterlockingManagementRouteInput
    {
        // 駅側で、この進路に必要な設備情報を取得できているか。
        // falseの進路を、正常な未設定として扱わない。
        public bool IsAvailable;

        public bool IsRouteSet;
        public bool PathEstablished;
        public bool ProceedAllowed;
        public bool RouteLocked;
        public bool CancelPending;
        public bool ApproachLocked;
        public float ApproachReleaseRemainingSeconds;

        public OverrunProtectionMode OverrunMode;
        public OverrunProtectionPhase OverrunPhase;
        public float OverrunReleaseRemainingSeconds;
    }
}
