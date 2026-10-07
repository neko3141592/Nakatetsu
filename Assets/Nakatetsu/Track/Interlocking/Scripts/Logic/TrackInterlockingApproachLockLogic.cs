using System;

namespace Nakatetsu.Track.Interlocking
{
    internal static class TrackInterlockingApproachLockLogic
    {
        internal static void Initialize(TrackInterlockingContext context)
        {
            context.State.approachLock.routes.Clear();
        }

        internal static void Register(TrackInterlockingContext context, string routeId)
        {
            context.State.approachLock.routes.Add(routeId, new TrackInterlockingApproachLockRecord());
        }

        internal static void Remove(TrackInterlockingContext context, string routeId)
        {
            context.State.approachLock.routes.Remove(routeId);
        }

        internal static void BeginCancel(TrackInterlockingContext context, string routeId)
        {
            var definition = context.Settings.routesById[routeId].approachLock;
            if (definition == null)
            {
                return;
            }

            foreach (string circuitId in definition.trackCircuitIds)
            {
                // 接近回路の取得不能も接近ありとして、取消時素を保持する。
                if (!context.Input.hasCircuitSource ||
                    !context.Input.OccupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied)
                {
                    var approach = context.State.approachLock.routes[routeId];
                    approach.isLocked = true;
                    approach.remainingSeconds = definition.releaseSeconds;
                    return;
                }
            }
        }

        internal static void Update(TrackInterlockingContext context, float deltaTimeSeconds)
        {
            if (!context.State.validation.hasValidTime)
            {
                return;
            }

            foreach (var approach in context.State.approachLock.routes.Values)
            {
                if (!approach.isLocked)
                {
                    continue;
                }

                approach.remainingSeconds = Math.Max(0f, approach.remainingSeconds - deltaTimeSeconds);
                approach.isLocked = approach.remainingSeconds > 0f;
            }
        }
    }
}
