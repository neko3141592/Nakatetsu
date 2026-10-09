using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking.Management
{
    public sealed class TrackInterlockingManagementOutput
    {
        public IReadOnlyDictionary<string, TrackInterlockingManagementStationStatus> StationsById { get; }

        internal TrackInterlockingManagementOutput() : this(new()) { }

        internal TrackInterlockingManagementOutput(
            Dictionary<string, TrackInterlockingManagementStationStatus> stations)
        {
            StationsById = new ReadOnlyDictionary<string, TrackInterlockingManagementStationStatus>(stations);
        }
    }

    public sealed class TrackInterlockingManagementStationStatus
    {
        public bool IsInitialized { get; }
        public bool HasOutput { get; }
        public IReadOnlyDictionary<string, TrackInterlockingManagementRouteStatus> RoutesById { get; }

        internal TrackInterlockingManagementStationStatus(TrackInterlockingManagementStationInput input,
            Dictionary<string, TrackInterlockingManagementRouteStatus> routes)
        {
            IsInitialized = input.IsInitialized;
            HasOutput = input.HasOutput;
            RoutesById = new ReadOnlyDictionary<string, TrackInterlockingManagementRouteStatus>(routes);
        }
    }

    public sealed class TrackInterlockingManagementRouteStatus
    {
        public bool IsAvailable { get; }
        public bool IsRouteSet { get; }
        public bool PathEstablished { get; }
        public bool ProceedAllowed { get; }
        public bool RouteLocked { get; }
        public bool CancelPending { get; }
        public bool ApproachLocked { get; }
        public float ApproachReleaseRemainingSeconds { get; }
        public OverrunProtectionMode OverrunMode { get; }
        public OverrunProtectionPhase OverrunPhase { get; }
        public float OverrunReleaseRemainingSeconds { get; }

        internal TrackInterlockingManagementRouteStatus(TrackInterlockingManagementRouteInput input)
        {
            IsAvailable = input.IsAvailable;
            IsRouteSet = input.IsRouteSet;
            PathEstablished = input.PathEstablished;
            ProceedAllowed = input.ProceedAllowed;
            RouteLocked = input.RouteLocked;
            CancelPending = input.CancelPending;
            ApproachLocked = input.ApproachLocked;
            ApproachReleaseRemainingSeconds = input.ApproachReleaseRemainingSeconds;
            OverrunMode = input.OverrunMode;
            OverrunPhase = input.OverrunPhase;
            OverrunReleaseRemainingSeconds = input.OverrunReleaseRemainingSeconds;
        }
    }
}
