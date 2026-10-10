#nullable enable

using System;
using System.Collections.Generic;

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>1駅の連動装置の公開状態。</summary>
    public sealed class InterlockingStationStatus
    {
        public bool IsInitialized { get; set; }
        public bool HasOutput { get; set; }

        public Dictionary<string, InterlockingRouteStatus> RoutesById { get; set; } =
            new Dictionary<string, InterlockingRouteStatus>(StringComparer.Ordinal);
    }
}
