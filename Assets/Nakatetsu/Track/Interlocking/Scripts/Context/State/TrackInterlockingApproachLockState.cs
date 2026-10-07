using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingApproachLockState
    {
        internal readonly Dictionary<string, TrackInterlockingApproachLockRecord> routes = new();
    }

    internal sealed class TrackInterlockingApproachLockRecord
    {
        internal bool isLocked;
        internal float remainingSeconds;
    }
}
