using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    internal static class TrackInterlockingOverrunProtectionLogic
    {
        internal static void Initialize(TrackInterlockingContext context)
        {
            context.State.overrunProtection.routes.Clear();
        }

        internal static void Register(TrackInterlockingContext context, string routeId, OverrunProtectionMode mode)
        {
            context.State.overrunProtection.routes.Add(routeId, new TrackInterlockingOverrunProtectionRecord
            {
                mode = mode,
                phase = mode == OverrunProtectionMode.Normal ? OverrunProtectionPhase.Setting :
                    mode == OverrunProtectionMode.Restricted ? OverrunProtectionPhase.Released : OverrunProtectionPhase.None
            });
        }

        internal static void Remove(TrackInterlockingContext context, string routeId)
        {
            context.State.overrunProtection.routes.Remove(routeId);
        }

        internal static void Update(TrackInterlockingContext context, float deltaTimeSeconds)
        {
            if (!context.State.validation.hasValidTime)
            {
                return;
            }

            foreach (var pair in context.State.overrunProtection.routes)
            {
                var protection = pair.Value;
                if (!protection.IsHeld)
                {
                    continue;
                }

                var route = context.Settings.routesById[pair.Key];
                var routeLock = context.State.routeLock.routes[pair.Key];
                var passage = context.State.passage.routes[pair.Key];
                bool approachLocked = context.State.approachLock.routes[pair.Key].isLocked;
                var turnouts = route.overrunProtection.turnoutRequirements;

                if (routeLock.cancelPending && !passage.hasEntered)
                {
                    // 未進入取消には防護時素を追加せず、転換動作の停止だけを待つ。
                    if (!routeLock.isLocked && !approachLocked && AreTurnoutsConfirmed(context.Input, turnouts, false))
                    {
                        Release(protection);
                    }
                    continue;
                }

                if (!routeLock.isLocked && routeLock.releaseReason == TrackInterlockingReleaseReason.Passage &&
                    protection.phase != OverrunProtectionPhase.ReleaseTiming)
                {
                    protection.phase = OverrunProtectionPhase.ReleaseTiming;
                    protection.remainingSeconds = route.overrunProtection.releaseSeconds;
                    if (protection.remainingSeconds == 0f && !approachLocked &&
                        AreTurnoutsConfirmed(context.Input, turnouts, true))
                    {
                        Release(protection);
                    }
                    // 本進路を解錠したtick以前の経過時間は、防護時素から引かない。
                    continue;
                }

                if (protection.phase == OverrunProtectionPhase.Setting)
                {
                    if (routeLock.isLocked && !routeLock.cancelPending && !passage.hasEntered &&
                        AreCircuitsClear(context.Input, route.routeClearTrackCircuitIds) &&
                        AreTurnoutsConfirmed(context.Input, turnouts, true))
                    {
                        protection.phase = OverrunProtectionPhase.Established;
                    }
                    continue;
                }

                if (protection.phase == OverrunProtectionPhase.ReleaseTiming)
                {
                    protection.remainingSeconds = Math.Max(0f, protection.remainingSeconds - deltaTimeSeconds);
                    if (protection.remainingSeconds == 0f && !routeLock.isLocked && !approachLocked &&
                        AreTurnoutsConfirmed(context.Input, turnouts, true))
                    {
                        Release(protection);
                    }
                }
            }
        }

        private static void Release(TrackInterlockingOverrunProtectionRecord protection)
        {
            protection.phase = OverrunProtectionPhase.Released;
            protection.remainingSeconds = 0f;
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

        private static bool AreTurnoutsConfirmed(TrackInterlockingInput input,
            List<TurnoutRequirement> turnouts, bool requirePosition)
        {
            foreach (var turnout in turnouts)
            {
                if (!input.hasConnectionSource ||
                    !input.ConnectionsById.TryGetValue(turnout.connectionId, out var actual) || actual.IsMoving ||
                    (actual.ActualPosition != TrackSwitchPosition.Normal &&
                     actual.ActualPosition != TrackSwitchPosition.Reverse) ||
                    (requirePosition && actual.ActualPosition != turnout.requiredPosition))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
