using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingRouteLockState
    {
        internal readonly Dictionary<string, TrackStationInterlockingRouteLockRecord> routes = new();
    }

    internal enum TrackStationInterlockingReleaseReason
    {
        None,
        Passage,
        Cancellation
    }

    internal sealed class TrackStationInterlockingRouteLockRecord
    {
        internal bool isLocked = true;
        internal bool cancelPending;
        internal TrackStationInterlockingReleaseReason releaseReason;
    }
}
