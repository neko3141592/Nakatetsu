namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>過走防護の段階。既存の駅連動装置と同じ値を使用する。</summary>
    public enum OverrunProtectionPhase
    {
        None = 0,
        Setting = 1,
        Established = 2,
        ReleaseTiming = 3,
        Released = 4
    }
}
