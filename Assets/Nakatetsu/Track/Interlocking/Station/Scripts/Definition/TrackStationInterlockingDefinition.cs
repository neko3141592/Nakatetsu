using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    [Serializable]
    public sealed class TrackStationInterlockingDefinition
    {
        public string interlockingId;

        public List<string> memberTrackCircuitIds = new();
        public List<string> memberConnectionIds = new();

        // 着点符号は駅内の論理的な着線・中間着点・出口を識別する。
        public List<TrackStationInterlockingDestinationDefinition> destinations = new();
        public List<TrackStationInterlockingRouteDefinition> routes = new();
        public List<TrackStationInterlockingTurnoutLockDefinition> turnoutLocks = new();


    }
}
