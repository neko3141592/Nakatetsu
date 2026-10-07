using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingInput
    {
        public bool hasCircuitSource;
        public bool hasConnectionSource;
        public readonly Dictionary<string, bool> OccupiedByCircuitId = new();
        public readonly Dictionary<string, TrackInterlockingTurnoutInput> ConnectionsById = new();
    }

    public readonly struct TrackInterlockingTurnoutInput
    {
        public TrackSwitchPosition ActualPosition { get; }
        public bool IsMoving { get; }

        public TrackInterlockingTurnoutInput(TrackSwitchPosition actualPosition, bool isMoving = false)
        {
            ActualPosition = actualPosition;
            IsMoving = isMoving;
        }
    }
}
