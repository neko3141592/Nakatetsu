namespace Nakatetsu.Track.Interlocking.Management
{
    public static class TrackInterlockingManagementInputAdapter
    {
        public static TrackInterlockingManagementStationInput CreateStationInput(
            bool isInitialized, TrackStationInterlockingOutput output)
        {
            var input = new TrackInterlockingManagementStationInput
            {
                IsInitialized = isInitialized,
                HasOutput = isInitialized && output != null
            };
            if (!input.HasOutput)
            {
                return input;
            }

            foreach (var pair in output.RoutesById)
            {
                var status = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key) || status == null)
                {
                    continue;
                }

                input.RoutesById.Add(pair.Key, new TrackInterlockingManagementRouteInput
                {
                    IsAvailable = status.IsAvailable,
                    IsRouteSet = status.IsRouteSet,
                    PathEstablished = status.PathEstablished,
                    ProceedAllowed = status.ProceedAllowed,
                    RouteLocked = status.RouteLocked,
                    CancelPending = status.CancelPending,
                    ApproachLocked = status.ApproachLocked,
                    ApproachReleaseRemainingSeconds = status.ApproachReleaseRemainingSeconds,
                    OverrunMode = status.OverrunMode,
                    OverrunPhase = status.OverrunPhase,
                    OverrunReleaseRemainingSeconds = status.OverrunReleaseRemainingSeconds
                });
            }
            return input;
        }
    }
}
