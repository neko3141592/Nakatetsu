#nullable enable

namespace Nakatetsu.Contracts.World
{
    /// <summary>
    /// Unity側からサーバーに渡す世界の時間情報
    /// </summary>
    public sealed class WorldTimeInformation
    {
        public long TickCount { get; set; }
        public float WorldTimeSeconds { get; set; }
        public float TickDurationSeconds { get; set; }

        public bool IsPaused { get; set; }
        public float PlaybackSpeed { get; set; }

    }
}
