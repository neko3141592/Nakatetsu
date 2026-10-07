namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingState
    {
        public bool isInitialized;
        public bool isInterlockingHealthy;
        public long stateRevision;
        public TrackInterlockingValidationState validation { get; } = new();
        public TrackInterlockingPassageState passage { get; } = new();
        public TrackInterlockingApproachLockState approachLock { get; } = new();
        public TrackInterlockingRouteLockState routeLock { get; } = new();
        public TrackInterlockingOverrunProtectionState overrunProtection { get; } = new();
        public TrackInterlockingReservationState reservation { get; } = new();
        public TrackInterlockingSignalState signal { get; } = new();
    }
}
