using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingSignalState
    {
        internal readonly Dictionary<string, TrackInterlockingSignalRecord> routes = new();
    }

    internal sealed class TrackInterlockingSignalRecord
    {
        internal bool isAvailable;
        internal bool pathEstablished;
        internal bool proceedAllowed;
        internal readonly List<TrackInterlockingTurnoutCommand> commands = new();
    }
}
