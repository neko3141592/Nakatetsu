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

    public sealed class TrackInterlockingOverrunProtectionState
    {
        internal readonly Dictionary<string, TrackInterlockingOverrunProtectionRecord> routes = new();
    }

    internal sealed class TrackInterlockingOverrunProtectionRecord
    {
        internal OverrunProtectionMode mode;
        internal OverrunProtectionPhase phase;
        internal float remainingSeconds;
        internal bool IsHeld => phase == OverrunProtectionPhase.Setting ||
            phase == OverrunProtectionPhase.Established || phase == OverrunProtectionPhase.ReleaseTiming;
    }
}
