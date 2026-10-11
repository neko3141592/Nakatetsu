#nullable enable

using System;
using System.Collections.Generic;
using MessagePack;

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>正常に取得できた全駅の連動状態。</summary>
    [MessagePackObject]
    public sealed class InterlockingSnapshot
    {
        // 登録済みの駅IDが含まれない場合、その駅の状態は取得できていない。
        [Key(0)]
        public Dictionary<string, InterlockingStationStatus> StationsById { get; set; } =
            new Dictionary<string, InterlockingStationStatus>(StringComparer.Ordinal);
    }
}
