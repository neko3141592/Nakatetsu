namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingState
    {
        public bool isInitialized;
        public bool isInterlockingHealthy;
        public long stateRevision;
        public TrackStationInterlockingValidationState validation { get; } = new();
        public TrackStationInterlockingPassageState passage { get; } = new();
        public TrackStationInterlockingApproachLockState approachLock { get; } = new();
        public TrackStationInterlockingRouteLockState routeLock { get; } = new();
        public TrackStationInterlockingOverrunProtectionState overrunProtection { get; } = new();
        public TrackStationInterlockingReservationState reservation { get; } = new();
        public TrackStationInterlockingSignalState signal { get; } = new();
    }
}
