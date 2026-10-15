using System;

namespace Nakatetsu.Track.Interlocking
{
    [Serializable]
    public sealed class TrackStationInterlockingDestinationDefinition
    {
        public string code;
        public string displayName;
        public string trackCircuitId;
    }
}
