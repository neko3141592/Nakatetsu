#nullable enable

using MessagePack;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Contracts.World;

namespace Nakatetsu.Contracts.Communication
{
    /// <summary>同じ完了Tickの世界時刻と連動状態をまとめた公開状態。</summary>
    [MessagePackObject]
    public sealed class UnitySnapshot
    {
        [Key(0)]
        public WorldTimeInformation WorldTime { get; set; } = new WorldTimeInformation();

        [Key(1)]
        public InterlockingSnapshot Interlocking { get; set; } = new InterlockingSnapshot();
    }
}
