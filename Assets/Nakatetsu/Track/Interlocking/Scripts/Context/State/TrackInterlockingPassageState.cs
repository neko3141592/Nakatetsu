using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public enum TrackInterlockingCircuitPassageState
    {
        NotEntered,
        Occupied,
        Passed
    }

    public sealed class TrackInterlockingPassageState
    {
        internal readonly Dictionary<string, TrackInterlockingPassageRecord> routes = new();
    }

    internal sealed class TrackInterlockingPassageRecord
    {
        internal bool hasEntered;
        internal readonly Dictionary<string, TrackInterlockingCircuitPassageState> circuits = new();
    }
}
