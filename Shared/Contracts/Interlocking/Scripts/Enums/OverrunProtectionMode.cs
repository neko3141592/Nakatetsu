namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>過走防護方式。既存の駅連動装置と同じ値を使用する。</summary>
    public enum OverrunProtectionMode
    {
        None = 0,
        Normal = 1,
        Restricted = 2
    }
}
