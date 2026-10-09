using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackStationInterlockingOutputLogic
    {
        internal static void UpdateOutput(TrackStationInterlockingContext context)
        {
            var state = context.State;
            var routes = new Dictionary<string, TrackStationInterlockingRouteStatus>();
            var commands = new List<TrackStationInterlockingTurnoutCommand>();
            foreach (var pair in state.signal.routes)
            {
                state.routeLock.routes.TryGetValue(pair.Key, out var route);
                state.approachLock.routes.TryGetValue(pair.Key, out var approach);
                state.overrunProtection.routes.TryGetValue(pair.Key, out var protection);
                state.passage.routes.TryGetValue(pair.Key, out var passage);
                routes.Add(pair.Key, new TrackStationInterlockingRouteStatus(pair.Key, pair.Value,
                    route, approach, protection, passage));
                commands.AddRange(pair.Value.commands);
            }
            context.Output = new TrackStationInterlockingOutput(state.stateRevision, routes, commands);
        }
    }
}
