using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingSettings
    {
        internal readonly Dictionary<string, TrackInterlockingRouteDefinition> routesById = new();
        internal readonly Dictionary<string, TrackInterlockingTurnoutLockDefinition> turnoutLocksById = new();
        internal TrackInterlockingDefinition definition;
        public IReadOnlyCollection<string> ConnectionIds { get; private set; } = new List<string>().AsReadOnly();

        internal static TrackInterlockingSettings CopyFrom(TrackInterlockingDefinition source)
        {
            var settings = new TrackInterlockingSettings();
            if (source == null)
            {
                return settings;
            }

            var copy = new TrackInterlockingDefinition
            {
                interlockingId = source.interlockingId,
                memberTrackCircuitIds = CopyIds(source.memberTrackCircuitIds),
                memberConnectionIds = CopyIds(source.memberConnectionIds),
                routes = source.routes == null ? null : new(),
                turnoutLocks = source.turnoutLocks == null ? null : new()
            };
            if (source.routes != null)
            {
                foreach (var route in source.routes)
                {
                    copy.routes.Add(route == null ? null : new TrackInterlockingRouteDefinition
                    {
                        routeId = route.routeId,
                        startTrackCircuitId = route.startTrackCircuitId,
                        destinationTrackCircuitId = route.destinationTrackCircuitId,
                        requiredTurnouts = CopyTurnouts(route.requiredTurnouts),
                        routeClearTrackCircuitIds = CopyIds(route.routeClearTrackCircuitIds),
                        routeReleaseTrackCircuitIds = CopyIds(route.routeReleaseTrackCircuitIds),
                        conflictRouteIds = CopyIds(route.conflictRouteIds),
                        approachLock = route.approachLock == null ? null : new ApproachLockDefinition
                        {
                            trackCircuitIds = CopyIds(route.approachLock.trackCircuitIds),
                            releaseSeconds = route.approachLock.releaseSeconds
                        },
                        overrunProtection = route.overrunProtection == null ? null : new TrackInterlockingOverrunProtectionDefinition
                        {
                            isEnabled = route.overrunProtection.isEnabled,
                            turnoutRequirements = CopyTurnouts(route.overrunProtection.turnoutRequirements),
                            releaseSeconds = route.overrunProtection.releaseSeconds
                        }
                    });
                }
            }
            if (source.turnoutLocks != null)
            {
                foreach (var turnoutLock in source.turnoutLocks)
                {
                    copy.turnoutLocks.Add(turnoutLock == null ? null : new TrackInterlockingTurnoutLockDefinition
                    {
                        connectionId = turnoutLock.connectionId,
                        trackCircuitIds = CopyIds(turnoutLock.trackCircuitIds)
                    });
                }
            }
            settings.definition = copy;
            settings.ConnectionIds = (copy.memberConnectionIds ?? new List<string>()).AsReadOnly();
            return settings;
        }

        private static List<string> CopyIds(List<string> source)
        {
            return source == null ? null : new List<string>(source);
        }

        private static List<TurnoutRequirement> CopyTurnouts(List<TurnoutRequirement> source)
        {
            if (source == null)
            {
                return null;
            }
            var result = new List<TurnoutRequirement>();
            foreach (var turnout in source)
            {
                result.Add(turnout == null ? null : new TurnoutRequirement
                {
                    connectionId = turnout.connectionId,
                    requiredPosition = turnout.requiredPosition
                });
            }
            return result;
        }
    }
}
