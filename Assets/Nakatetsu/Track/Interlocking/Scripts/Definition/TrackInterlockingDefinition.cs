using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    [Serializable]
    public sealed class TrackInterlockingDefinition
    {
        public string interlockingId;

        public List<string> memberTrackCircuitIds = new();
        public List<string> memberConnectionIds = new();

        public List<TrackInterlockingRouteDefinition> routes = new();
        public List<TrackInterlockingTurnoutLockDefinition> turnoutLocks = new();


    }
}