using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingOutput
    {
        public long StateRevision { get; }
        public IReadOnlyDictionary<string, TrackInterlockingRouteStatus> RoutesById { get; }
        public IReadOnlyList<TrackInterlockingTurnoutCommand> TurnoutCommands { get; }

        internal TrackInterlockingOutput() : this(0, new(), new()) { }

        internal TrackInterlockingOutput(long revision,
            Dictionary<string, TrackInterlockingRouteStatus> routes,
            List<TrackInterlockingTurnoutCommand> commands)
        {
            StateRevision = revision;
            RoutesById = new ReadOnlyDictionary<string, TrackInterlockingRouteStatus>(routes);
            TurnoutCommands = commands.AsReadOnly();
        }
    }

    public sealed class TrackInterlockingRouteStatus
    {
        public string RouteId { get; }
        public bool IsAvailable { get; }
        public bool IsRouteSet { get; }
        public bool RouteLocked { get; }
        public bool CancelPending { get; }
        public bool ApproachLocked { get; }
        public float ApproachReleaseRemainingSeconds { get; }
        public bool PathEstablished { get; }
        public bool ProceedAllowed { get; }
        public OverrunProtectionMode OverrunMode { get; }
        public OverrunProtectionPhase OverrunPhase { get; }
        public float OverrunReleaseRemainingSeconds { get; }
        public IReadOnlyDictionary<string, TrackInterlockingCircuitPassageState> CircuitPassageById { get; }

        internal TrackInterlockingRouteStatus(string routeId, TrackInterlockingSignalRecord signal,
            TrackInterlockingRouteLockRecord route, TrackInterlockingApproachLockRecord approach,
            TrackInterlockingOverrunProtectionRecord protection, TrackInterlockingPassageRecord passage)
        {
            RouteId = routeId;
            IsAvailable = signal.isAvailable;
            IsRouteSet = route != null;
            RouteLocked = route?.isLocked ?? false;
            CancelPending = route?.cancelPending ?? false;
            ApproachLocked = approach?.isLocked ?? false;
            ApproachReleaseRemainingSeconds = approach?.remainingSeconds ?? 0f;
            PathEstablished = signal.pathEstablished;
            ProceedAllowed = signal.proceedAllowed;
            OverrunMode = protection?.mode ?? OverrunProtectionMode.None;
            OverrunPhase = protection?.phase ?? OverrunProtectionPhase.None;
            OverrunReleaseRemainingSeconds = protection?.remainingSeconds ?? 0f;
            CircuitPassageById = new ReadOnlyDictionary<string, TrackInterlockingCircuitPassageState>(
                passage == null ? new() : new Dictionary<string, TrackInterlockingCircuitPassageState>(passage.circuits));
        }
    }

    public readonly struct TrackInterlockingTurnoutCommand
    {
        public string RouteId { get; }
        public string ConnectionId { get; }
        public TrackSwitchPosition RequiredPosition { get; }

        internal TrackInterlockingTurnoutCommand(string routeId, TurnoutRequirement turnout)
        {
            RouteId = routeId;
            ConnectionId = turnout.connectionId;
            RequiredPosition = turnout.requiredPosition;
        }
    }
}
