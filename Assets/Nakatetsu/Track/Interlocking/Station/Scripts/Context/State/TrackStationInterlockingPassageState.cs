using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public enum TrackStationInterlockingCircuitPassageState
    {
        NotEntered,
        Occupied,
        Passed
    }

    public sealed class TrackStationInterlockingPassageState
    {
        internal readonly Dictionary<string, TrackStationInterlockingPassageRecord> routes = new();
    }

    internal sealed class TrackStationInterlockingPassageRecord
    {
        internal bool hasEntered;
        internal readonly Dictionary<string, TrackStationInterlockingCircuitPassageState> circuits = new();
    }
}
