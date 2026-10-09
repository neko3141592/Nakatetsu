using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking.Management
{
    public static class TrackInterlockingManagementOutputLogic
    {
        internal static void ResetOutput(TrackInterlockingManagementContext context)
        {
            context.Output = new TrackInterlockingManagementOutput();
        }

        internal static void UpdateOutput(TrackInterlockingManagementContext context)
        {
            var stations = new Dictionary<string, TrackInterlockingManagementStationStatus>();
            if (context.State.IsInitialized)
            {
                CollectStations(context.Input, stations);
            }

            // 今回取得できなかった駅を残さず、Inputとは独立したスナップショットを公開する。
            context.Output = new TrackInterlockingManagementOutput(stations);
        }

        private static void CollectStations(TrackInterlockingManagementInput input,
            Dictionary<string, TrackInterlockingManagementStationStatus> stations)
        {
            foreach (var pair in input.StationsById)
            {
                var station = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key) || station == null ||
                    !station.IsInitialized || !station.HasOutput)
                {
                    continue;
                }

                var routes = CopyRoutes(station);
                stations.Add(pair.Key, new TrackInterlockingManagementStationStatus(station, routes));
            }
        }

        private static Dictionary<string, TrackInterlockingManagementRouteStatus> CopyRoutes(
            TrackInterlockingManagementStationInput station)
        {
            var routes = new Dictionary<string, TrackInterlockingManagementRouteStatus>();
            foreach (var pair in station.RoutesById)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                // IsAvailable=falseも、駅から取得できた進路の状態として保持する。
                routes.Add(pair.Key, new TrackInterlockingManagementRouteStatus(pair.Value));
            }
            return routes;
        }
    }
}
