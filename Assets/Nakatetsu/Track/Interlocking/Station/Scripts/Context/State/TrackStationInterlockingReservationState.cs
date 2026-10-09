using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingReservationState
    {
        internal readonly Dictionary<string, List<TrackStationInterlockingEquipmentHold>> circuits = new();
        internal readonly Dictionary<string, List<TrackStationInterlockingEquipmentHold>> turnouts = new();
    }

    internal enum TrackStationInterlockingHoldReason
    {
        MainRoute,
        OverrunProtection
    }

    internal readonly struct TrackStationInterlockingEquipmentHold
    {
        internal string RouteId { get; }
        internal TrackStationInterlockingHoldReason Reason { get; }
        internal TrackSwitchPosition Position { get; }

        internal TrackStationInterlockingEquipmentHold(string routeId, TrackStationInterlockingHoldReason reason,
            TrackSwitchPosition position = TrackSwitchPosition.Unknown)
        {
            RouteId = routeId;
            Reason = reason;
            Position = position;
        }
    }
}
