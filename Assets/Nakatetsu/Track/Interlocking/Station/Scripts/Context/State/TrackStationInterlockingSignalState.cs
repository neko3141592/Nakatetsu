using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingSignalState
    {
        internal readonly Dictionary<string, TrackStationInterlockingSignalRecord> routes = new();
    }

    internal sealed class TrackStationInterlockingSignalRecord
    {
        internal bool isAvailable;
        internal bool pathEstablished;
        internal bool proceedAllowed;
        internal readonly List<TrackStationInterlockingTurnoutCommand> commands = new();
    }
}
