using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingInput
    {
        public bool hasCircuitSource;
        public bool hasConnectionSource;
        public readonly Dictionary<string, bool> OccupiedByCircuitId = new();
        public readonly Dictionary<string, TrackStationInterlockingTurnoutInput> ConnectionsById = new();
    }

    public readonly struct TrackStationInterlockingTurnoutInput
    {
        public TrackSwitchPosition ActualPosition { get; }
        public bool IsMoving { get; }

        public TrackStationInterlockingTurnoutInput(TrackSwitchPosition actualPosition, bool isMoving = false)
        {
            ActualPosition = actualPosition;
            IsMoving = isMoving;
        }
    }
}
