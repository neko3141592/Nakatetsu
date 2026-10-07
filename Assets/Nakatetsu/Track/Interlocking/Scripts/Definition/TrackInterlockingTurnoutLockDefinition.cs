using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    // 轍査鎖錠
    [Serializable]
    public sealed class TrackInterlockingTurnoutLockDefinition
    {
        public string connectionId;

        // この軌道回路に列車が在線している場合は対象転轍機の転換要求を拒否
        public List<string> trackCircuitIds = new();
    }
}