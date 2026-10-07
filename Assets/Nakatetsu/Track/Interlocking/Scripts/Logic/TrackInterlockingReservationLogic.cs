using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackInterlockingReservationLogic
    {
        internal static void Initialize(TrackInterlockingContext context)
        {
            context.State.reservation.circuits.Clear();
            context.State.reservation.turnouts.Clear();
        }

        internal static bool CanReserveMain(TrackInterlockingContext context, string routeId, out string error)
        {
            TrackInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            foreach (string circuitId in route.routeClearTrackCircuitIds)
            {
                if (!context.Input.hasCircuitSource ||
                    !context.Input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied)
                {
                    error = $"Track circuit '{circuitId}' is occupied or unavailable.";
                    return false;
                }

                if (context.State.reservation.circuits.TryGetValue(circuitId, out var holds) && holds.Count > 0)
                {
                    error = $"Track circuit '{circuitId}' is reserved.";
                    return false;
                }
            }

            foreach (var pair in context.State.routeLock.routes)
            {
                if (pair.Key == routeId)
                {
                    continue;
                }

                bool approachHeld = context.State.approachLock.routes.TryGetValue(pair.Key, out var approach) &&
                    approach.isLocked;
                if (!pair.Value.isLocked && !approachHeld)
                {
                    continue;
                }

                TrackInterlockingRouteDefinition existing = context.Settings.routesById[pair.Key];
                if (route.conflictRouteIds.Contains(pair.Key) || existing.conflictRouteIds.Contains(routeId))
                {
                    error = $"Route '{routeId}' conflicts with '{pair.Key}'.";
                    return false;
                }
            }

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                if (HasOppositeTurnoutHold(context.State.reservation, turnout))
                {
                    error = $"Turnout '{turnout.connectionId}' is reserved at another position.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal static bool CanReserveProtection(TrackInterlockingContext context, string routeId)
        {
            TrackInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            foreach (TurnoutRequirement turnout in route.overrunProtection.turnoutRequirements)
            {
                if (HasOppositeTurnoutHold(context.State.reservation, turnout))
                {
                    return false;
                }
            }
            return true;
        }

        internal static void Register(TrackInterlockingContext context, string routeId, OverrunProtectionMode mode)
        {
            TrackInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            TrackInterlockingReservationState reservation = context.State.reservation;
            foreach (string circuitId in route.routeClearTrackCircuitIds)
            {
                AddHold(reservation.circuits, circuitId,
                    new TrackInterlockingEquipmentHold(routeId, TrackInterlockingHoldReason.MainRoute));
            }

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                AddHold(reservation.turnouts, turnout.connectionId,
                    new TrackInterlockingEquipmentHold(routeId, TrackInterlockingHoldReason.MainRoute,
                        turnout.requiredPosition));
            }

            if (mode == OverrunProtectionMode.Normal)
            {
                foreach (TurnoutRequirement turnout in route.overrunProtection.turnoutRequirements)
                {
                    AddHold(reservation.turnouts, turnout.connectionId,
                        new TrackInterlockingEquipmentHold(routeId, TrackInterlockingHoldReason.OverrunProtection,
                            turnout.requiredPosition));
                }
            }
        }

        internal static void Update(TrackInterlockingContext context)
        {
            RemoveReleasedHolds(context, context.State.reservation.circuits);
            RemoveReleasedHolds(context, context.State.reservation.turnouts);
        }

        internal static bool HasReservations(TrackInterlockingContext context, string routeId)
        {
            return HasRouteHold(context.State.reservation.circuits, routeId) ||
                HasRouteHold(context.State.reservation.turnouts, routeId);
        }

        internal static void Remove(TrackInterlockingContext context, string routeId)
        {
            RemoveRouteHolds(context.State.reservation.circuits, routeId);
            RemoveRouteHolds(context.State.reservation.turnouts, routeId);
        }

        private static bool HasOppositeTurnoutHold(TrackInterlockingReservationState reservation,
            TurnoutRequirement turnout)
        {
            if (!reservation.turnouts.TryGetValue(turnout.connectionId, out var holds))
            {
                return false;
            }
            foreach (TrackInterlockingEquipmentHold hold in holds)
            {
                if (hold.Position != turnout.requiredPosition)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddHold(Dictionary<string, List<TrackInterlockingEquipmentHold>> holdsById,
            string equipmentId, TrackInterlockingEquipmentHold hold)
        {
            if (!holdsById.TryGetValue(equipmentId, out var holds))
            {
                holds = new List<TrackInterlockingEquipmentHold>();
                holdsById.Add(equipmentId, holds);
            }
            holds.Add(hold);
        }

        private static void RemoveReleasedHolds(TrackInterlockingContext context,
            Dictionary<string, List<TrackInterlockingEquipmentHold>> holdsById)
        {
            foreach (string equipmentId in new List<string>(holdsById.Keys))
            {
                var holds = holdsById[equipmentId];
                holds.RemoveAll(hold => !IsHeld(context, hold));
                if (holds.Count == 0)
                {
                    holdsById.Remove(equipmentId);
                }
            }
        }

        private static bool IsHeld(TrackInterlockingContext context, TrackInterlockingEquipmentHold hold)
        {
            if (hold.Reason == TrackInterlockingHoldReason.OverrunProtection)
            {
                return context.State.overrunProtection.routes.TryGetValue(hold.RouteId, out var protection) &&
                    protection.IsHeld;
            }

            bool routeHeld = context.State.routeLock.routes.TryGetValue(hold.RouteId, out var route) && route.isLocked;
            bool approachHeld = context.State.approachLock.routes.TryGetValue(hold.RouteId, out var approach) &&
                approach.isLocked;
            return routeHeld || approachHeld;
        }

        private static bool HasRouteHold(Dictionary<string, List<TrackInterlockingEquipmentHold>> holdsById,
            string routeId)
        {
            foreach (var holds in holdsById.Values)
            {
                foreach (TrackInterlockingEquipmentHold hold in holds)
                {
                    if (hold.RouteId == routeId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void RemoveRouteHolds(Dictionary<string, List<TrackInterlockingEquipmentHold>> holdsById,
            string routeId)
        {
            foreach (string equipmentId in new List<string>(holdsById.Keys))
            {
                var holds = holdsById[equipmentId];
                holds.RemoveAll(hold => hold.RouteId == routeId);
                if (holds.Count == 0)
                {
                    holdsById.Remove(equipmentId);
                }
            }
        }
    }
}
