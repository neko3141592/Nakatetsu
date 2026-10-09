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

        public List<TrackStationInterlockingRouteDefinition> routes = new();
        public List<TrackStationInterlockingTurnoutLockDefinition> turnoutLocks = new();


    }
}