using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    internal static class TrackInterlockingRouteLockLogic
    {
        internal static void Initialize(TrackInterlockingContext context)
        {
            context.State.routeLock.routes.Clear();
        }

        internal static void Register(TrackInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes.Add(routeId, new TrackInterlockingRouteLockRecord());
        }

        internal static void Remove(TrackInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes.Remove(routeId);
        }

        internal static void RecordCancel(TrackInterlockingContext context, string routeId)
        {
            context.State.routeLock.routes[routeId].cancelPending = true;
        }

        internal static void Update(TrackInterlockingContext context)
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
                    routeLock.releaseReason = TrackInterlockingReleaseReason.Passage;
                }
                else if (routeLock.cancelPending && !passage.hasEntered &&
                    AreCircuitsClear(context.Input, route.routeClearTrackCircuitIds) &&
                    AreTurnoutsStopped(context.Input, route.requiredTurnouts))
                {
                    // 未進入取消では、要求位置に戻さず現在の確定位置で停止すればよい。
                    routeLock.isLocked = false;
                    routeLock.releaseReason = TrackInterlockingReleaseReason.Cancellation;
                }
            }
        }

        private static bool HasRearPassed(TrackInterlockingInput input, List<string> circuitIds,
            TrackInterlockingPassageRecord passage)
        {
            if (!input.hasCircuitSource || circuitIds.Count == 0)
            {
                return false;
            }

            foreach (string circuitId in circuitIds)
            {
                if (!input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied ||
                    passage.circuits[circuitId] != TrackInterlockingCircuitPassageState.Passed)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreCircuitsClear(TrackInterlockingInput input, List<string> circuitIds)
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

        private static bool AreTurnoutsStopped(TrackInterlockingInput input, List<TurnoutRequirement> turnouts)
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
