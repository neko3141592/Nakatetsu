namespace Nakatetsu.Track.Interlocking
{
    internal static class TrackInterlockingPassageLogic
    {
        internal static void Initialize(TrackInterlockingContext context)
        {
            context.State.passage.routes.Clear();
        }

        internal static void Register(TrackInterlockingContext context, string routeId)
        {
            var passage = new TrackInterlockingPassageRecord();
            foreach (string circuitId in context.Settings.routesById[routeId].routeClearTrackCircuitIds)
            {
                passage.circuits[circuitId] = TrackInterlockingCircuitPassageState.NotEntered;
            }
            context.State.passage.routes.Add(routeId, passage);
        }

        internal static void Remove(TrackInterlockingContext context, string routeId)
        {
            context.State.passage.routes.Remove(routeId);
        }

        internal static void Update(TrackInterlockingContext context)
        {
            if (!context.Input.hasCircuitSource || !context.State.validation.hasValidTime)
            {
                return;
            }

            foreach (var pair in context.State.passage.routes)
            {
                // 本進路解錠後は、別列車の在線をこの予約の通過履歴へ取り込まない。
                if (!context.State.routeLock.routes[pair.Key].isLocked)
                {
                    continue;
                }

                var passage = pair.Value;
                foreach (string circuitId in context.Settings.routesById[pair.Key].routeClearTrackCircuitIds)
                {
                    if (!context.Input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied))
                    {
                        continue;
                    }

                    if (occupied)
                    {
                        passage.hasEntered = true;
                        passage.circuits[circuitId] = TrackInterlockingCircuitPassageState.Occupied;
                    }
                    else if (passage.circuits[circuitId] == TrackInterlockingCircuitPassageState.Occupied)
                    {
                        passage.circuits[circuitId] = TrackInterlockingCircuitPassageState.Passed;
                    }
                }
            }
        }
    }
}
