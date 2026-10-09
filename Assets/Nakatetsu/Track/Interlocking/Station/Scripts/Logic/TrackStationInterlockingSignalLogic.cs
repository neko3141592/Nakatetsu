using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackStationInterlockingSignalLogic
    {
        internal static void Initialize(TrackStationInterlockingContext context)
        {
            context.State.signal.routes.Clear();
            foreach (string routeId in context.Settings.routesById.Keys)
            {
                context.State.signal.routes.Add(routeId, new TrackStationInterlockingSignalRecord());
            }
        }

        internal static void Register(TrackStationInterlockingContext context, string routeId)
        {
            ResetReservation(context, routeId);
        }

        internal static void ResetReservation(TrackStationInterlockingContext context, string routeId)
        {
            TrackStationInterlockingSignalRecord signal = context.State.signal.routes[routeId];
            signal.pathEstablished = false;
            signal.proceedAllowed = false;
            signal.commands.Clear();
        }

        internal static void Update(TrackStationInterlockingContext context, bool allowEstablish)
        {
            var commandedConnections = new HashSet<string>();
            foreach (var pair in context.Settings.routesById)
            {
                TrackStationInterlockingSignalRecord signal = context.State.signal.routes[pair.Key];
                signal.isAvailable = context.State.validation.availableByRouteId.TryGetValue(pair.Key,
                    out bool available) && available;

                bool candidatePath = signal.isAvailable && IsPathEstablished(context, pair.Key, pair.Value);
                bool candidateProceed = candidatePath && CanProceed(context, pair.Key, pair.Value);
                signal.pathEstablished = candidatePath && (allowEstablish || signal.pathEstablished);
                signal.proceedAllowed = candidateProceed && (allowEstablish || signal.proceedAllowed);

                if (allowEstablish)
                {
                    signal.commands.Clear();
                    if (signal.isAvailable)
                    {
                        AddTurnoutCommands(context, pair.Key, pair.Value, signal.commands, commandedConnections);
                    }
                }
                else
                {
                    // 操作APIでは既存の転換要求を撤回するだけで、新しい要求は作らない。
                    for (int i = 0; i < signal.commands.Count;)
                    {
                        TrackStationInterlockingTurnoutCommand command = signal.commands[i];
                        if (!signal.isAvailable ||
                            !CanRequestTurnoutPosition(context, pair.Key, command.ConnectionId, command.RequiredPosition) ||
                            !commandedConnections.Add(command.ConnectionId))
                        {
                            signal.commands.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }
        }

        internal static bool CanRequestTurnoutPosition(TrackStationInterlockingContext context,
            string routeId, string connectionId, TrackSwitchPosition requiredPosition)
        {
            if (!context.State.isInitialized || !context.Input.hasCircuitSource || !context.Input.hasConnectionSource ||
                !context.Settings.routesById.TryGetValue(routeId, out var definition) ||
                !context.State.routeLock.routes.TryGetValue(routeId, out var route) || !route.isLocked ||
                route.cancelPending || !context.State.passage.routes.TryGetValue(routeId, out var passage) ||
                passage.hasEntered || !AreCircuitsClear(context.Input, definition.routeClearTrackCircuitIds) ||
                !HasRequiredTurnoutInputs(context, routeId, definition))
            {
                return false;
            }

            if (!context.Input.ConnectionsById.TryGetValue(connectionId, out var turnout) || turnout.IsMoving ||
                !IsConfirmedPosition(turnout.ActualPosition) || turnout.ActualPosition == requiredPosition ||
                !context.Settings.turnoutLocksById.TryGetValue(connectionId, out var turnoutLock) ||
                !AreCircuitsClear(context.Input, turnoutLock.trackCircuitIds))
            {
                return false;
            }

            if (!context.State.reservation.turnouts.TryGetValue(connectionId, out var holds))
            {
                return false;
            }
            foreach (TrackStationInterlockingEquipmentHold hold in holds)
            {
                if (hold.RouteId == routeId && hold.Position == requiredPosition)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPathEstablished(TrackStationInterlockingContext context, string routeId,
            TrackStationInterlockingRouteDefinition definition)
        {
            if (!context.State.routeLock.routes.TryGetValue(routeId, out var route) || !route.isLocked ||
                !AreTurnoutsEstablished(context.Input, definition.requiredTurnouts))
            {
                return false;
            }

            if (!context.State.overrunProtection.routes.TryGetValue(routeId, out var protection))
            {
                return false;
            }
            if (protection.mode == OverrunProtectionMode.Normal)
            {
                if (protection.phase == OverrunProtectionPhase.Setting || protection.phase == OverrunProtectionPhase.None)
                {
                    return false;
                }
                if (protection.IsHeld &&
                    !AreTurnoutsEstablished(context.Input, definition.overrunProtection.turnoutRequirements))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CanProceed(TrackStationInterlockingContext context, string routeId,
            TrackStationInterlockingRouteDefinition definition)
        {
            return !context.State.routeLock.routes[routeId].cancelPending &&
                !context.State.passage.routes[routeId].hasEntered &&
                AreCircuitsClear(context.Input, definition.routeClearTrackCircuitIds);
        }

        private static void AddTurnoutCommands(TrackStationInterlockingContext context, string routeId,
            TrackStationInterlockingRouteDefinition definition, List<TrackStationInterlockingTurnoutCommand> commands,
            HashSet<string> commandedConnections)
        {
            foreach (TurnoutRequirement turnout in definition.requiredTurnouts)
            {
                AddTurnoutCommand(context, routeId, turnout, commands, commandedConnections);
            }

            if (context.State.overrunProtection.routes.TryGetValue(routeId, out var protection) && protection.IsHeld)
            {
                foreach (TurnoutRequirement turnout in definition.overrunProtection.turnoutRequirements)
                {
                    AddTurnoutCommand(context, routeId, turnout, commands, commandedConnections);
                }
            }
        }

        private static void AddTurnoutCommand(TrackStationInterlockingContext context, string routeId,
            TurnoutRequirement turnout, List<TrackStationInterlockingTurnoutCommand> commands,
            HashSet<string> commandedConnections)
        {
            if (CanRequestTurnoutPosition(context, routeId, turnout.connectionId, turnout.requiredPosition) &&
                commandedConnections.Add(turnout.connectionId))
            {
                commands.Add(new TrackStationInterlockingTurnoutCommand(routeId, turnout));
            }
        }

        private static bool AreTurnoutsEstablished(TrackStationInterlockingInput input, List<TurnoutRequirement> turnouts)
        {
            foreach (TurnoutRequirement required in turnouts)
            {
                if (!input.ConnectionsById.TryGetValue(required.connectionId, out var actual) ||
                    actual.IsMoving || actual.ActualPosition != required.requiredPosition)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasRequiredTurnoutInputs(TrackStationInterlockingContext context, string routeId,
            TrackStationInterlockingRouteDefinition definition)
        {
            // Calculate後の入力欠落も、転換要求を送る直前に照査する。
            if (!HasTurnoutInputs(context.Input, definition.requiredTurnouts))
            {
                return false;
            }
            return !context.State.overrunProtection.routes.TryGetValue(routeId, out var protection) ||
                protection.mode != OverrunProtectionMode.Normal || !protection.IsHeld ||
                HasTurnoutInputs(context.Input, definition.overrunProtection.turnoutRequirements);
        }

        private static bool HasTurnoutInputs(TrackStationInterlockingInput input, List<TurnoutRequirement> turnouts)
        {
            foreach (TurnoutRequirement required in turnouts)
            {
                if (!input.ConnectionsById.TryGetValue(required.connectionId, out var actual) ||
                    (!IsConfirmedPosition(actual.ActualPosition) &&
                     !(actual.ActualPosition == TrackSwitchPosition.Unknown && actual.IsMoving)))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreCircuitsClear(TrackStationInterlockingInput input, List<string> circuitIds)
        {
            foreach (string circuitId in circuitIds)
            {
                if (!input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsConfirmedPosition(TrackSwitchPosition position)
        {
            return position == TrackSwitchPosition.Normal || position == TrackSwitchPosition.Reverse;
        }
    }
}
