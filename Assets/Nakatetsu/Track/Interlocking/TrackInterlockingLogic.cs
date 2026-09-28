using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackInterlockingLogic
    {
        public static bool TryInitialize(TrackInterlockingContext context,
            TrackInterlockingDefinition definition, out string error)
        {
            context.RoutesById.Clear();
            context.RouteStatesById.Clear();
            context.TurnoutTrackCircuitLocksByConnectionId.Clear();
            context.IsInitialized = false;

            if (definition?.routes == null || definition.turnoutTrackCircuitLocks == null)
            {
                error = "Interlocking definition is missing.";
                return false;
            }

            foreach (InterlockingRoute route in definition.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.routeId) ||
                    !context.RoutesById.TryAdd(route.routeId, route))
                {
                    context.RoutesById.Clear();
                    error = "Route IDs must be nonempty and unique.";
                    return false;
                }
            }

            foreach (TurnoutTrackCircuitLock turnoutLock in definition.turnoutTrackCircuitLocks)
            {
                if (turnoutLock == null || string.IsNullOrWhiteSpace(turnoutLock.connectionId) ||
                    turnoutLock.trackCircuitIds == null || turnoutLock.trackCircuitIds.Count == 0 ||
                    !context.TurnoutTrackCircuitLocksByConnectionId.TryAdd(turnoutLock.connectionId, turnoutLock))
                {
                    error = "Turnout track circuit locks must have unique connection IDs and at least one circuit.";
                    return false;
                }

                foreach (string circuitId in turnoutLock.trackCircuitIds)
                {
                    if (!string.IsNullOrWhiteSpace(circuitId)) continue;
                    error = $"Turnout '{turnoutLock.connectionId}' has an empty track circuit ID.";
                    return false;
                }
            }

            foreach (InterlockingRoute route in definition.routes)
            {
                if (route.requiredTurnouts == null) continue;
                foreach (TurnoutRequirement turnout in route.requiredTurnouts)
                {
                    if (turnout != null && !string.IsNullOrWhiteSpace(turnout.connectionId) &&
                        context.TurnoutTrackCircuitLocksByConnectionId.ContainsKey(turnout.connectionId))
                        continue;

                    error = $"Route '{route.routeId}' has a turnout without a track circuit lock.";
                    return false;
                }
            }

            context.IsInitialized = true;
            error = null;
            return true;
        }

        public static bool CanRequestTurnoutPosition(TrackInterlockingContext context,
            TrackCircuitSimulationState circuits, string routeId, string connectionId)
        {
            if (context == null || !context.IsInitialized || circuits == null ||
                string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(connectionId) ||
                !context.RoutesById.TryGetValue(routeId, out InterlockingRoute route) ||
                !context.RouteStatesById.TryGetValue(routeId, out TrackInterlockingRouteState state) ||
                !CanSetRouteTurnouts(route, circuits, state) ||
                route.requiredTurnouts == null ||
                !context.TurnoutTrackCircuitLocksByConnectionId.TryGetValue(connectionId, out var turnoutLock))
                return false;

            bool requiredByRoute = false;
            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
                requiredByRoute |= turnout != null && turnout.connectionId == connectionId;
            if (!requiredByRoute) return false;

            if (turnoutLock.trackCircuitIds == null || turnoutLock.trackCircuitIds.Count == 0)
                return false;

            foreach (string circuitId in turnoutLock.trackCircuitIds)
                if (circuits.IsOccupied(circuitId)) return false;

            return true;
        }

        public static bool TryRequestRoute(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            string routeId,
            out string error)
        {
            if (context == null || !context.IsInitialized ||
                string.IsNullOrWhiteSpace(routeId) ||
                !context.RoutesById.TryGetValue(routeId, out InterlockingRoute route))
            {
                error = $"Route '{routeId}' is unavailable or interlocking is not initialized.";
                return false;
            }

            if (context.RouteStatesById.ContainsKey(routeId))
            {
                error = $"Route '{routeId}' is already requested.";
                return false;
            }

            if (circuits == null || connections == null || !connections.IsInitialized)
            {
                error = "Track circuits or connections are unavailable.";
                return false;
            }

            if (route.routeLockTrackCircuitIds == null || route.routeLockTrackCircuitIds.Count == 0 ||
                route.requiredTurnouts == null)
            {
                error = $"Route '{routeId}' has an incomplete definition.";
                return false;
            }

            var routeCircuitIds = new HashSet<string>();
            foreach (string circuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(circuitId) || !routeCircuitIds.Add(circuitId))
                {
                    error = $"Route '{routeId}' has an invalid or duplicate track circuit ID.";
                    return false;
                }

                if (circuits.IsOccupied(circuitId))
                {
                    error = $"Track circuit '{circuitId}' is occupied or unavailable.";
                    return false;
                }
            }

            var requiredPositions = new Dictionary<string, TrackSwitchPosition>();
            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId) ||
                    (turnout.requiredPosition != TrackSwitchPosition.Normal &&
                     turnout.requiredPosition != TrackSwitchPosition.Reverse) ||
                    (requiredPositions.TryGetValue(turnout.connectionId, out var position) &&
                     position != turnout.requiredPosition))
                {
                    error = $"Route '{routeId}' has an invalid turnout requirement.";
                    return false;
                }

                requiredPositions[turnout.connectionId] = turnout.requiredPosition;
                if (!connections.TryGetState(turnout.connectionId, out _))
                {
                    error = $"Switch '{turnout.connectionId}' is unavailable.";
                    return false;
                }
            }

            foreach (var active in context.RouteStatesById)
            {
                if (!context.RoutesById.TryGetValue(active.Key, out InterlockingRoute activeRoute) ||
                    activeRoute.routeLockTrackCircuitIds == null || activeRoute.requiredTurnouts == null)
                {
                    error = $"Active route '{active.Key}' has an incomplete definition.";
                    return false;
                }

                if ((route.conflictRouteIds != null && route.conflictRouteIds.Contains(active.Key)) ||
                    (activeRoute.conflictRouteIds != null && activeRoute.conflictRouteIds.Contains(routeId)))
                {
                    error = $"Route '{routeId}' conflicts with active route '{active.Key}'.";
                    return false;
                }

                foreach (string circuitId in activeRoute.routeLockTrackCircuitIds)
                {
                    if (routeCircuitIds.Contains(circuitId))
                    {
                        error = $"Track circuit '{circuitId}' is reserved by route '{active.Key}'.";
                        return false;
                    }
                }

                foreach (TurnoutRequirement turnout in activeRoute.requiredTurnouts)
                {
                    if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId))
                    {
                        error = $"Active route '{active.Key}' has an invalid turnout requirement.";
                        return false;
                    }

                    if (requiredPositions.TryGetValue(turnout.connectionId, out var position) &&
                        position != turnout.requiredPosition)
                    {
                        error = $"Switch '{turnout.connectionId}' is locked in another position by route '{active.Key}'.";
                        return false;
                    }
                }
            }

            var state = new TrackInterlockingRouteState { RouteLocked = true };
            foreach (string circuitId in routeCircuitIds)
                state.CircuitPassageById.Add(circuitId, TrackInterlockingCircuitPassageState.NotEntered);

            context.RouteStatesById.Add(routeId, state);
            error = null;
            return true;
        }

        public static bool TryCancelRoute(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            string routeId,
            out string error)
        {
            if (context == null || !context.IsInitialized ||
                string.IsNullOrWhiteSpace(routeId) ||
                !context.RouteStatesById.TryGetValue(routeId, out var state) ||
                !context.RoutesById.TryGetValue(routeId, out var route))
            {
                error = $"Route '{routeId}' is not active.";
                return false;
            }

            if (state.CancelPending)
            {
                error = null;
                return true;
            }

            if (circuits == null)
            {
                error = "Track circuits are unavailable.";
                return false;
            }

            state.CancelPending = true;
            state.ProceedAllowed = false;

            if (route.approachLock?.trackCircuitIds != null)
            {
                foreach (string circuitId in route.approachLock.trackCircuitIds)
                {
                    if (!circuits.IsOccupied(circuitId)) continue;

                    state.ApproachLocked = true;
                    state.ApproachReleaseRemainingSeconds = Mathf.Max(0f, route.approachLock.releaseSeconds);
                    break;
                }
            }

            error = null;
            return true;
        }

        public static void Calculate(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            float deltaTimeSeconds)
        {
            var routeIdsToDelete = new List<string>();
            foreach (var pair in context.RouteStatesById)
            {
                string routeId = pair.Key;
                TrackInterlockingRouteState state = pair.Value;

                InterlockingRoute route = context.RoutesById[routeId];

                UpdateTrackCircuitState(
                    route,
                    circuits,
                    state
                );

                UpdateApproachLockState(
                    state,
                    deltaTimeSeconds
                );

                UpdateRouteLockState(
                    route,
                    circuits,
                    state
                );

                UpdateProceedAllow(
                    route,
                    circuits,
                    connections,
                    state
                );

                if (CanDeleteRouteState(state))
                    routeIdsToDelete.Add(routeId);

            }

            foreach (string routeId in routeIdsToDelete)
                context.RouteStatesById.Remove(routeId);
        }

        public static void UpdateTrackCircuitState(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state
        )
        {
            if (route.routeLockTrackCircuitIds == null || circuits == null)
            {
                return;
            }

            foreach (string trackCircuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(trackCircuitId) ||
                    !state.CircuitPassageById.TryGetValue(trackCircuitId, out var passageState) ||
                    !circuits.OccupiedByCircuitId.TryGetValue(trackCircuitId, out bool occupied))
                {
                    continue;
                }

                if (occupied)
                {
                    state.CircuitPassageById[trackCircuitId] = TrackInterlockingCircuitPassageState.Occupied;
                    state.RouteLocked = true;
                }
                else if (passageState == TrackInterlockingCircuitPassageState.Occupied)
                {
                    state.CircuitPassageById[trackCircuitId] = TrackInterlockingCircuitPassageState.Passed;
                }
            }
        }

        public static void UpdateApproachLockState(
            TrackInterlockingRouteState state,
            float deltaTimeSeconds
        )
        {
            if (!state.ApproachLocked)
            {
                return;
            }

            state.ApproachReleaseRemainingSeconds = Mathf.Max(
                0f,
                state.ApproachReleaseRemainingSeconds - deltaTimeSeconds
            );

            if (state.ApproachReleaseRemainingSeconds == 0f)
            {
                state.ApproachLocked = false;
            }

        }

        public static void UpdateRouteLockState(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state
        )
        {
            if (!state.RouteLocked || circuits == null || route.routeLockTrackCircuitIds == null ||
                route.routeLockTrackCircuitIds.Count == 0)
            {
                return;
            }

            bool allPassed = true;
            bool allNotEntered = true;
            foreach (string trackCircuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(trackCircuitId) ||
                    !state.CircuitPassageById.TryGetValue(trackCircuitId, out var passageState) ||
                    circuits.IsOccupied(trackCircuitId))
                {
                    return;
                }

                allPassed &= passageState == TrackInterlockingCircuitPassageState.Passed;
                allNotEntered &= passageState == TrackInterlockingCircuitPassageState.NotEntered;
            }


            if (allPassed || (state.CancelPending && allNotEntered))
                state.RouteLocked = false;
        }

        public static void UpdateProceedAllow(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state
        )
        {
            state.ProceedAllowed = false;
            if (!CanSetRouteTurnouts(route, circuits, state) ||
                connections == null || route.requiredTurnouts == null)
                return;

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                if (turnout == null || turnout.requiredPosition == TrackSwitchPosition.Unknown ||
                    !connections.TryGetState(turnout.connectionId, out TrackConnectionState switchState) ||
                    switchState.IsMoving || switchState.ActualPosition != turnout.requiredPosition)
                    return;
            }

            state.ProceedAllowed = true;
        }

        internal static bool CanSetRouteTurnouts(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state)
        {
            if (!state.RouteLocked || state.CancelPending || circuits == null ||
                route.routeLockTrackCircuitIds == null || route.routeLockTrackCircuitIds.Count == 0)
                return false;

            // 取消後や列車進入後は、進行許可がなくても転轍しない。
            foreach (string circuitId in route.routeLockTrackCircuitIds)
            {
                if (!state.CircuitPassageById.TryGetValue(circuitId, out var passage) ||
                    passage != TrackInterlockingCircuitPassageState.NotEntered ||
                    circuits.IsOccupied(circuitId))
                    return false;
            }

            return true;
        }

        private static bool CanDeleteRouteState(TrackInterlockingRouteState state) =>
            !state.ProceedAllowed && !state.ApproachLocked && !state.RouteLocked;
    }
}
