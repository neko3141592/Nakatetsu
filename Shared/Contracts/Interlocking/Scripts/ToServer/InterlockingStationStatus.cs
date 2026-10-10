#nullable enable

using System;
using System.Collections.Generic;
using MessagePack;

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>1駅の連動装置の公開状態。</summary>
    [MessagePackObject]
    public sealed class InterlockingStationStatus
    {
        [Key(0)]
        public bool IsInitialized { get; set; }

        [Key(1)]
        public bool HasOutput { get; set; }

        [Key(2)]
        public Dictionary<string, InterlockingRouteStatus> RoutesById { get; set; } =
            new Dictionary<string, InterlockingRouteStatus>(StringComparer.Ordinal);
    }
}
