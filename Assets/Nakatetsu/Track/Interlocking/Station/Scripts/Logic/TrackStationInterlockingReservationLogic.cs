using System.Collections.Generic;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
using OverrunProtectionMode = Nakatetsu.Track.Simulation.Circuit.OverrunProtectionMode;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackStationInterlockingReservationLogic
    {
        internal static void Initialize(TrackStationInterlockingContext context)
        {
            context.State.reservation.circuits.Clear();
            context.State.reservation.turnouts.Clear();
        }

        internal static bool CanReserveMain(TrackStationInterlockingContext context,
            InterlockingRouteRequest request, out InterlockingRouteRequestResult error)
        {
            string routeId = request.RouteId;
            TrackStationInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            foreach (string circuitId in route.routeClearTrackCircuitIds)
            {
                if (!context.Input.hasCircuitSource ||
                    !context.Input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied))
                {
                    error = InterlockingRouteRequestResultUtility.CreateInputUnavailableError(request,
                        $"Track circuit '{circuitId}' is unavailable.", circuitId);
                    return false;
                }
                if (occupied)
                {
                    error = InterlockingRouteRequestResultUtility.CreateCircuitOccupiedError(request,
                        circuitId, $"Track circuit '{circuitId}' is occupied.");
                    return false;
                }

                if (context.State.reservation.circuits.TryGetValue(circuitId, out var holds) && holds.Count > 0)
                {
                    error = InterlockingRouteRequestResultUtility.CreateCircuitReservedError(request,
                        circuitId, holds[0].RouteId, $"Track circuit '{circuitId}' is reserved.");
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

                TrackStationInterlockingRouteDefinition existing = context.Settings.routesById[pair.Key];
                if (route.conflictRouteIds.Contains(pair.Key) || existing.conflictRouteIds.Contains(routeId))
                {
                    error = InterlockingRouteRequestResultUtility.CreateRouteConflictError(request,
                        pair.Key, $"Route '{routeId}' conflicts with '{pair.Key}'.");
                    return false;
                }
            }

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                if (TryGetOppositeTurnoutHold(context.State.reservation, turnout, out var hold))
                {
                    error = InterlockingRouteRequestResultUtility.CreateTurnoutPositionConflictError(request,
                        turnout.connectionId, hold.RouteId,
                        $"Turnout '{turnout.connectionId}' is reserved at another position.");
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal static bool CanReserveProtection(TrackStationInterlockingContext context, string routeId)
        {
            TrackStationInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            foreach (TurnoutRequirement turnout in route.overrunProtection.turnoutRequirements)
            {
                if (HasOppositeTurnoutHold(context.State.reservation, turnout))
                {
                    return false;
                }
            }
            return true;
        }

        internal static void Register(TrackStationInterlockingContext context, string routeId, OverrunProtectionMode mode)
        {
            TrackStationInterlockingRouteDefinition route = context.Settings.routesById[routeId];
            TrackStationInterlockingReservationState reservation = context.State.reservation;
            foreach (string circuitId in route.routeClearTrackCircuitIds)
            {
                AddHold(reservation.circuits, circuitId,
                    new TrackStationInterlockingEquipmentHold(routeId, TrackStationInterlockingHoldReason.MainRoute));
            }

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                AddHold(reservation.turnouts, turnout.connectionId,
                    new TrackStationInterlockingEquipmentHold(routeId, TrackStationInterlockingHoldReason.MainRoute,
                        turnout.requiredPosition));
            }

            if (mode == OverrunProtectionMode.Normal)
            {
                foreach (TurnoutRequirement turnout in route.overrunProtection.turnoutRequirements)
                {
                    AddHold(reservation.turnouts, turnout.connectionId,
                        new TrackStationInterlockingEquipmentHold(routeId, TrackStationInterlockingHoldReason.OverrunProtection,
                            turnout.requiredPosition));
                }
            }
        }

        internal static void Update(TrackStationInterlockingContext context)
        {
            RemoveReleasedHolds(context, context.State.reservation.circuits);
            RemoveReleasedHolds(context, context.State.reservation.turnouts);
        }

        internal static bool HasReservations(TrackStationInterlockingContext context, string routeId)
        {
            return HasRouteHold(context.State.reservation.circuits, routeId) ||
                HasRouteHold(context.State.reservation.turnouts, routeId);
        }

        internal static void Remove(TrackStationInterlockingContext context, string routeId)
        {
            RemoveRouteHolds(context.State.reservation.circuits, routeId);
            RemoveRouteHolds(context.State.reservation.turnouts, routeId);
        }

        private static bool HasOppositeTurnoutHold(TrackStationInterlockingReservationState reservation,
            TurnoutRequirement turnout)
        {
            return TryGetOppositeTurnoutHold(reservation, turnout, out _);
        }

        private static bool TryGetOppositeTurnoutHold(TrackStationInterlockingReservationState reservation,
            TurnoutRequirement turnout, out TrackStationInterlockingEquipmentHold oppositeHold)
        {
            oppositeHold = default;
            if (!reservation.turnouts.TryGetValue(turnout.connectionId, out var holds))
            {
                return false;
            }
            foreach (TrackStationInterlockingEquipmentHold hold in holds)
            {
                if (hold.Position != turnout.requiredPosition)
                {
                    oppositeHold = hold;
                    return true;
                }
            }
            return false;
        }

        private static void AddHold(Dictionary<string, List<TrackStationInterlockingEquipmentHold>> holdsById,
            string equipmentId, TrackStationInterlockingEquipmentHold hold)
        {
            if (!holdsById.TryGetValue(equipmentId, out var holds))
            {
                holds = new List<TrackStationInterlockingEquipmentHold>();
                holdsById.Add(equipmentId, holds);
            }
            holds.Add(hold);
        }

        private static void RemoveReleasedHolds(TrackStationInterlockingContext context,
            Dictionary<string, List<TrackStationInterlockingEquipmentHold>> holdsById)
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

        private static bool IsHeld(TrackStationInterlockingContext context, TrackStationInterlockingEquipmentHold hold)
        {
            if (hold.Reason == TrackStationInterlockingHoldReason.OverrunProtection)
            {
                return context.State.overrunProtection.routes.TryGetValue(hold.RouteId, out var protection) &&
                    protection.IsHeld;
            }

            bool routeHeld = context.State.routeLock.routes.TryGetValue(hold.RouteId, out var route) && route.isLocked;
            bool approachHeld = context.State.approachLock.routes.TryGetValue(hold.RouteId, out var approach) &&
                approach.isLocked;
            return routeHeld || approachHeld;
        }

        private static bool HasRouteHold(Dictionary<string, List<TrackStationInterlockingEquipmentHold>> holdsById,
            string routeId)
        {
            foreach (var holds in holdsById.Values)
            {
                foreach (TrackStationInterlockingEquipmentHold hold in holds)
                {
                    if (hold.RouteId == routeId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void RemoveRouteHolds(Dictionary<string, List<TrackStationInterlockingEquipmentHold>> holdsById,
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
