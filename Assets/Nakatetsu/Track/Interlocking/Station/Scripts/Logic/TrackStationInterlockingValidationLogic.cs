using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackStationInterlockingValidationLogic
    {
        internal static bool TryPrepareSettings(TrackStationInterlockingSettings settings, out string error)
        {
            error = string.Empty;
            var definition = settings.definition;
            if (definition == null || definition.memberTrackCircuitIds == null ||
                definition.memberConnectionIds == null || definition.routes == null || definition.turnoutLocks == null)
            {
                error = "連動装置の定義を取得できない。";
                return false;
            }

            var circuits = new HashSet<string>(definition.memberTrackCircuitIds);
            var connections = new HashSet<string>(definition.memberConnectionIds);
            foreach (var turnoutLock in definition.turnoutLocks)
            {
                if (turnoutLock == null || string.IsNullOrWhiteSpace(turnoutLock.connectionId) ||
                    !connections.Contains(turnoutLock.connectionId) ||
                    !ContainsIds(circuits, turnoutLock.trackCircuitIds) ||
                    !settings.turnoutLocksById.TryAdd(turnoutLock.connectionId, turnoutLock))
                {
                    error = "轍査鎖錠の定義が不正。";
                    return false;
                }
            }

            foreach (var route in definition.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.routeId) ||
                    !circuits.Contains(route.startTrackCircuitId) || !circuits.Contains(route.destinationTrackCircuitId) ||
                    !ContainsIds(circuits, route.routeClearTrackCircuitIds) ||
                    !ContainsIds(new HashSet<string>(route.routeClearTrackCircuitIds), route.routeReleaseTrackCircuitIds) ||
                    route.routeReleaseTrackCircuitIds.Count == 0 || route.conflictRouteIds == null ||
                    !ValidTurnouts(settings, route.requiredTurnouts))
                {
                    error = "進路のID、参照回路、または転轍機定義が不正。";
                    return false;
                }

                if (route.approachLock != null &&
                    (!ContainsIds(circuits, route.approachLock.trackCircuitIds) || !ValidSeconds(route.approachLock.releaseSeconds)))
                {
                    error = $"進路 '{route.routeId}' の接近鎖錠定義が不正。";
                    return false;
                }

                var protection = route.overrunProtection;
                if (protection != null && protection.isEnabled)
                {
                    if (!ValidTurnouts(settings, protection.turnoutRequirements) || !ValidSeconds(protection.releaseSeconds))
                    {
                        error = $"進路 '{route.routeId}' の過走防護定義が不正。";
                        return false;
                    }
                    foreach (var main in route.requiredTurnouts)
                    {
                        foreach (var additional in protection.turnoutRequirements)
                        {
                            if (main.connectionId == additional.connectionId && main.requiredPosition != additional.requiredPosition)
                            {
                                error = $"進路 '{route.routeId}' に同時に保持できない転轍機開通方向がある。";
                                return false;
                            }
                        }
                    }
                }

                if (!settings.routesById.TryAdd(route.routeId, route))
                {
                    error = $"進路ID '{route.routeId}' が重複している。";
                    return false;
                }
            }

            foreach (var route in settings.routesById.Values)
            {
                foreach (string conflictId in route.conflictRouteIds)
                {
                    if (string.IsNullOrWhiteSpace(conflictId) || !settings.routesById.ContainsKey(conflictId))
                    {
                        error = $"進路 '{route.routeId}' の競合進路を取得できない。";
                        return false;
                    }
                }
            }
            return true;
        }

        internal static void Initialize(TrackStationInterlockingContext context)
        {
            var state = context.State.validation;
            state.isDefinitionValid = true;
            state.hasValidTime = true;
            state.availableByRouteId.Clear();
        }

        internal static void Update(TrackStationInterlockingContext context, float deltaTimeSeconds)
        {
            var state = context.State.validation;
            state.hasValidTime = ValidSeconds(deltaTimeSeconds);
            state.availableByRouteId.Clear();
            foreach (var route in context.Settings.routesById.Values)
            {
                bool available = state.hasValidTime && context.Input.hasCircuitSource && context.Input.hasConnectionSource;
                foreach (string circuitId in route.routeClearTrackCircuitIds)
                {
                    available &= context.Input.OccupiedByCircuitId.ContainsKey(circuitId);
                }
                available &= HasTurnoutInputs(context.Input, route.requiredTurnouts);
                if (context.State.overrunProtection.routes.TryGetValue(route.routeId, out var protection) &&
                    protection.mode == OverrunProtectionMode.Normal && protection.IsHeld)
                {
                    available &= HasTurnoutInputs(context.Input, route.overrunProtection.turnoutRequirements);
                }
                state.availableByRouteId[route.routeId] = available;
            }
        }

        internal static bool HasProtectionInput(TrackStationInterlockingContext context, TrackStationInterlockingRouteDefinition route)
        {
            return route.overrunProtection == null || !route.overrunProtection.isEnabled ||
                HasTurnoutInputs(context.Input, route.overrunProtection.turnoutRequirements);
        }

        internal static bool TryGetUnavailableRouteInput(TrackStationInterlockingContext context,
            TrackStationInterlockingRouteDefinition route, out string circuitId, out string turnoutId)
        {
            circuitId = null;
            turnoutId = null;
            foreach (string requiredCircuitId in route.routeClearTrackCircuitIds)
            {
                if (!context.Input.hasCircuitSource ||
                    !context.Input.OccupiedByCircuitId.ContainsKey(requiredCircuitId))
                {
                    circuitId = requiredCircuitId;
                    return true;
                }
            }
            if (TryGetUnavailableTurnoutInput(context.Input, route.requiredTurnouts, out turnoutId))
            {
                return true;
            }
            return route.overrunProtection != null && route.overrunProtection.isEnabled &&
                TryGetUnavailableTurnoutInput(context.Input, route.overrunProtection.turnoutRequirements,
                    out turnoutId);
        }

        private static bool HasTurnoutInputs(TrackStationInterlockingInput input, List<TurnoutRequirement> turnouts)
        {
            return !TryGetUnavailableTurnoutInput(input, turnouts, out _);
        }

        private static bool TryGetUnavailableTurnoutInput(TrackStationInterlockingInput input,
            List<TurnoutRequirement> turnouts, out string turnoutId)
        {
            turnoutId = null;
            foreach (var turnout in turnouts)
            {
                if (!input.hasConnectionSource || !input.ConnectionsById.TryGetValue(turnout.connectionId, out var value) ||
                    (value.ActualPosition != TrackSwitchPosition.Normal && value.ActualPosition != TrackSwitchPosition.Reverse &&
                     !(value.ActualPosition == TrackSwitchPosition.Unknown && value.IsMoving)))
                {
                    turnoutId = turnout.connectionId;
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsIds(HashSet<string> members, List<string> ids)
        {
            if (ids == null)
            {
                return false;
            }
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !members.Contains(id))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValidTurnouts(TrackStationInterlockingSettings settings, List<TurnoutRequirement> turnouts)
        {
            if (turnouts == null)
            {
                return false;
            }
            var ids = new HashSet<string>();
            foreach (var turnout in turnouts)
            {
                if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId) ||
                    !settings.turnoutLocksById.ContainsKey(turnout.connectionId) || !ids.Add(turnout.connectionId) ||
                    (turnout.requiredPosition != TrackSwitchPosition.Normal && turnout.requiredPosition != TrackSwitchPosition.Reverse))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValidSeconds(float seconds)
        {
            return !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f;
        }
    }
}
