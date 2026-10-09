using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    internal static class TrackStationInterlockingRouteLockLogic
    {
        internal static void Initialize(TrackStationInterlockingContext context)
        {
            context.State.routeLock.routes.Clear();
        }

        internal static void Register(TrackStationInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes.Add(routeId, new TrackStationInterlockingRouteLockRecord());
        }

        internal static void Remove(TrackStationInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes.Remove(routeId);
        }

        internal static void RecordCancel(TrackStationInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes[routeId].cancelPending = true;
        }

        internal static void Update(TrackStationInterlockingContext context)
        {
            if (!context.State.validation.hasValidTime)
            {
                return;
            }

            foreach (var pair in context.State.routeLock.routes)
            {
                var routeLock = pair.Value;
                if (!routeLock.isLocked || context.State.approachLock.routes[pair.Key].isLocked)
                {
                    continue;
                }

                var route = context.Settings.routesById[pair.Key];
                var passage = context.State.passage.routes[pair.Key];
                if (HasRearPassed(context.Input, route.routeReleaseTrackCircuitIds, passage))
                {
                    routeLock.isLocked = false;
                    routeLock.releaseReason = TrackStationInterlockingReleaseReason.Passage;
                }
                else if (routeLock.cancelPending && !passage.hasEntered &&
                    AreCircuitsClear(context.Input, route.routeClearTrackCircuitIds) &&
                    AreTurnoutsStopped(context.Input, route.requiredTurnouts))
                {
                    // 未進入取消では、要求位置に戻さず現在の確定位置で停止すればよい。
                    routeLock.isLocked = false;
                    routeLock.releaseReason = TrackStationInterlockingReleaseReason.Cancellation;
                }
            }
        }

        private static bool HasRearPassed(TrackStationInterlockingInput input, List<string> circuitIds,
            TrackStationInterlockingPassageRecord passage)
        {
            if (!input.hasCircuitSource || circuitIds.Count == 0)
            {
                return false;
            }

            foreach (string circuitId in circuitIds)
            {
                if (!input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied ||
                    passage.circuits[circuitId] != TrackStationInterlockingCircuitPassageState.Passed)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreCircuitsClear(TrackStationInterlockingInput input, List<string> circuitIds)
        {
            if (!input.hasCircuitSource)
            {
                return false;
            }

            foreach (string circuitId in circuitIds)
            {
                if (!input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreTurnoutsStopped(TrackStationInterlockingInput input, List<TurnoutRequirement> turnouts)
        {
            foreach (var turnout in turnouts)
            {
                if (!input.hasConnectionSource ||
                    !input.ConnectionsById.TryGetValue(turnout.connectionId, out var actual) || actual.IsMoving ||
                    (actual.ActualPosition != TrackSwitchPosition.Normal &&
                     actual.ActualPosition != TrackSwitchPosition.Reverse))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
