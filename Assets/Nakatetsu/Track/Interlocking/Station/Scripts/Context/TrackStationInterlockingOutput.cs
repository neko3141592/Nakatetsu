using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;

namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingOutput
    {
        public long StateRevision { get; }
        public IReadOnlyDictionary<string, TrackStationInterlockingRouteStatus> RoutesById { get; }
        public IReadOnlyList<TrackStationInterlockingTurnoutCommand> TurnoutCommands { get; }

        internal TrackStationInterlockingOutput() : this(0, new(), new()) { }

        internal TrackStationInterlockingOutput(long revision,
            Dictionary<string, TrackStationInterlockingRouteStatus> routes,
            List<TrackStationInterlockingTurnoutCommand> commands)
        {
            StateRevision = revision;
            RoutesById = new ReadOnlyDictionary<string, TrackStationInterlockingRouteStatus>(routes);
            TurnoutCommands = commands.AsReadOnly();
        }
    }

    public sealed class TrackStationInterlockingRouteStatus
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
        public IReadOnlyDictionary<string, TrackStationInterlockingCircuitPassageState> CircuitPassageById { get; }

        internal TrackStationInterlockingRouteStatus(string routeId, TrackStationInterlockingSignalRecord signal,
            TrackStationInterlockingRouteLockRecord route, TrackStationInterlockingApproachLockRecord approach,
            TrackStationInterlockingOverrunProtectionRecord protection, TrackStationInterlockingPassageRecord passage)
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
            CircuitPassageById = new ReadOnlyDictionary<string, TrackStationInterlockingCircuitPassageState>(
                passage == null ? new() : new Dictionary<string, TrackStationInterlockingCircuitPassageState>(passage.circuits));
        }
    }

    public readonly struct TrackStationInterlockingTurnoutCommand
    {
        public string RouteId { get; }
        public string ConnectionId { get; }
        public TrackSwitchPosition RequiredPosition { get; }

        internal TrackStationInterlockingTurnoutCommand(string routeId, TurnoutRequirement turnout)
        {
            RouteId = routeId;
            ConnectionId = turnout.connectionId;
            RequiredPosition = turnout.requiredPosition;
        }
    }
}
