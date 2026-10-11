#nullable enable

using MessagePack;

namespace Nakatetsu.Contracts.World
{
    /// <summary>
    /// Unity側からサーバーに渡す世界の時間情報
    /// </summary>
    [MessagePackObject]
    public sealed class WorldTimeInformation
    {
        [Key(0)]
        public long TickCount { get; set; }

        [Key(1)]
        public float WorldTimeSeconds { get; set; }

        [Key(2)]
        public float TickDurationSeconds { get; set; }

        [Key(3)]
        public bool IsPaused { get; set; }

        [Key(4)]
        public float PlaybackSpeed { get; set; }

    }
}
