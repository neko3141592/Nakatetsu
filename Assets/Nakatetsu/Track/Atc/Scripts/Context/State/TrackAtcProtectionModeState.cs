using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    public sealed class TrackAtcProtectionModeState
    {
        // 工程3と同じ起点キーに対する、今回の防護方式の確定結果。
        // 前回の方式は保持せず、工程4で毎更新置き換える。
        public Dictionary<TrackAtcEdgeKey, TrackAtcProtectionModeResult> resultsByKey = new();
    }

    public sealed class TrackAtcProtectionModeResult
    {
        // 初期値は未確定のfalse。Noneを取得失敗の代わりに使わない。
        public bool isProtectionModeKnown;
        public OverrunProtectionMode overrunProtectionMode;

        // 方式を確定できない理由。成否は上のフラグで判定する。
        public string failureReason = string.Empty;
    }
}
