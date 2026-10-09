using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingApproachLockState
    {
        internal readonly Dictionary<string, TrackStationInterlockingApproachLockRecord> routes = new();
    }

    internal sealed class TrackStationInterlockingApproachLockRecord
    {
        internal bool isLocked;
        internal float remainingSeconds;
    }
}
