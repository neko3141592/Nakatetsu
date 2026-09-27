namespace Nakatetsu.Core.Time
{
    /// <summary>時間を進めず、シミュレーションの世界時刻を読み取る。</summary>
    public interface IWorldTimeSource
    {
        double WorldTimeSeconds { get; }
    }
}
