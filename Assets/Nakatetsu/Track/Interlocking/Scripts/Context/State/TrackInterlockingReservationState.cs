using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingReservationState
    {
        internal readonly Dictionary<string, List<TrackInterlockingEquipmentHold>> circuits = new();
        internal readonly Dictionary<string, List<TrackInterlockingEquipmentHold>> turnouts = new();
    }

    internal enum TrackInterlockingHoldReason
    {
        MainRoute,
        OverrunProtection
    }

    internal readonly struct TrackInterlockingEquipmentHold
    {
        internal string RouteId { get; }
        internal TrackInterlockingHoldReason Reason { get; }
        internal TrackSwitchPosition Position { get; }

        internal TrackInterlockingEquipmentHold(string routeId, TrackInterlockingHoldReason reason,
            TrackSwitchPosition position = TrackSwitchPosition.Unknown)
        {
            RouteId = routeId;
            Reason = reason;
            Position = position;
        }
    }
}
