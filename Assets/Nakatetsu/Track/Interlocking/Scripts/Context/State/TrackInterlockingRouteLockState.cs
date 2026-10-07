using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingRouteLockState
    {
        internal readonly Dictionary<string, TrackInterlockingRouteLockRecord> routes = new();
    }

    internal enum TrackInterlockingReleaseReason
    {
        None,
        Passage,
        Cancellation
    }

    internal sealed class TrackInterlockingRouteLockRecord
    {
        internal bool isLocked = true;
        internal bool cancelPending;
        internal TrackInterlockingReleaseReason releaseReason;
    }
}
