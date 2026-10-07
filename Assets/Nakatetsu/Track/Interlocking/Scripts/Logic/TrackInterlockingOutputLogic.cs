using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackInterlockingOutputLogic
    {
        internal static void UpdateOutput(TrackInterlockingContext context)
        {
            var state = context.State;
            var routes = new Dictionary<string, TrackInterlockingRouteStatus>();
            var commands = new List<TrackInterlockingTurnoutCommand>();
            foreach (var pair in state.signal.routes)
            {
                state.routeLock.routes.TryGetValue(pair.Key, out var route);
                state.approachLock.routes.TryGetValue(pair.Key, out var approach);
                state.overrunProtection.routes.TryGetValue(pair.Key, out var protection);
                state.passage.routes.TryGetValue(pair.Key, out var passage);
                routes.Add(pair.Key, new TrackInterlockingRouteStatus(pair.Key, pair.Value,
                    route, approach, protection, passage));
                commands.AddRange(pair.Value.commands);
            }
            context.Output = new TrackInterlockingOutput(state.stateRevision, routes, commands);
        }
    }
}
