using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking
{
    public enum OverrunProtectionPhase
    {
        None,
        Setting,
        Established,
        ReleaseTiming,
        Released
    }

    public sealed class TrackStationInterlockingOverrunProtectionState
    {
        internal readonly Dictionary<string, TrackStationInterlockingOverrunProtectionRecord> routes = new();
    }

    internal sealed class TrackStationInterlockingOverrunProtectionRecord
    {
        internal OverrunProtectionMode mode;
        internal OverrunProtectionPhase phase;
        internal float remainingSeconds;
        internal bool IsHeld => phase == OverrunProtectionPhase.Setting ||
            phase == OverrunProtectionPhase.Established || phase == OverrunProtectionPhase.ReleaseTiming;
    }
}
