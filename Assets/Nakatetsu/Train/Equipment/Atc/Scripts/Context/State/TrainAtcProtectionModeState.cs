using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcProtectionModeState
    {
        // 今回使用する防護方式を確定できたか。正常なNoneと取得失敗を区別する。
        public bool isProtectionModeKnown;

        // 防護方式はここだけで管理する。パターン保持時はこの値を維持する。
        // isProtectionModeKnownがfalseの場合は、この値を使用しない。
        public OverrunProtectionMode overrunProtectionMode;
    }
}
