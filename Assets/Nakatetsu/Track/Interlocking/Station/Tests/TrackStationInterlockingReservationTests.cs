using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using InterlockingRouteErrorCode = Nakatetsu.Contracts.Interlocking.InterlockingRouteErrorCode;
using InterlockingRouteOperation = Nakatetsu.Contracts.Interlocking.InterlockingRouteOperation;
using InterlockingRouteRequest = Nakatetsu.Contracts.Interlocking.InterlockingRouteRequest;
using InterlockingRouteRequestResult = Nakatetsu.Contracts.Interlocking.InterlockingRouteRequestResult;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackStationInterlockingReservationTests : TrackStationInterlockingTestFixture
    {
        [Test]
        public void RequestReservesEquipmentButWaitsForTickToEstablishPath()
        {
            Initialize();
            Request();

            var pending = Status();
            Assert.That(pending.IsRouteSet, Is.True);
            Assert.That(pending.RouteLocked, Is.True);
            Assert.That(pending.OverrunMode, Is.EqualTo(OverrunProtectionMode.Normal));
            Assert.That(pending.OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Setting));
            Assert.That(pending.PathEstablished, Is.False);
            Assert.That(pending.ProceedAllowed, Is.False);

            Tick();
            Assert.That(Status().PathEstablished, Is.True);
            Assert.That(Status().ProceedAllowed, Is.True);
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Established));
        }

        [Test]
        public void OccupiedPlatformRejectsRequestEvenWhenPlatformIsNotReleaseCircuit()
        {
            SetOccupied("Platform", true);
            Initialize();

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest());
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.CircuitOccupied));
            Assert.That(result.RelatedCircuitId, Is.EqualTo("Platform"));
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void MainCircuitReservationRejectsAnotherRouteWithoutPartialReservation()
        {
            Initialize();
            Request();

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest("ReuseMain"));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.CircuitReserved));
            Assert.That(result.RelatedCircuitId, Is.EqualTo("Entry"));
            Assert.That(result.RelatedRouteId, Is.EqualTo(ArrivalRouteId));
            Assert.That(Status("ReuseMain").IsRouteSet, Is.False);
            Assert.That(Status().RouteLocked, Is.True);
            Assert.That(Status().OverrunMode, Is.EqualTo(OverrunProtectionMode.Normal));
        }

        [TestCase(TrackSwitchPosition.Normal, true)]
        [TestCase(TrackSwitchPosition.Reverse, false)]
        public void RequiredTurnoutCanBeSharedOnlyAtSamePosition(TrackSwitchPosition position, bool accepted)
        {
            var other = definition.routes[1];
            other.routeClearTrackCircuitIds = new List<string> { "Alternate" };
            other.routeReleaseTrackCircuitIds = new List<string> { "Alternate" };
            other.startTrackCircuitId = "Alternate";
            other.requiredTurnouts[0].requiredPosition = position;
            Initialize();
            Request();

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest("ReuseMain"));
            Assert.That(result.Accepted, Is.EqualTo(accepted));
            Assert.That(result.ErrorCode, Is.EqualTo(accepted
                ? InterlockingRouteErrorCode.None : InterlockingRouteErrorCode.TurnoutPositionConflict));
            if (!accepted)
            {
                Assert.That(result.RelatedTurnoutId, Is.EqualTo("Main"));
                Assert.That(result.RelatedRouteId, Is.EqualTo(ArrivalRouteId));
            }
            Assert.That(Status("ReuseMain").IsRouteSet, Is.EqualTo(accepted));
            Assert.That(Status().RouteLocked, Is.True);
        }

        [Test]
        public void ProtectionTurnoutConflictSelectsRestrictedAndDoesNotUpgradeAutomatically()
        {
            Initialize();
            Request("ReuseProtection");
            var result = Request();
            Tick();

            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.None));
            Assert.That(Status().OverrunMode, Is.EqualTo(OverrunProtectionMode.Restricted));
            Assert.That(Status().PathEstablished, Is.True);
            Assert.That(Status().ProceedAllowed, Is.True);

            Cancel("ReuseProtection");
            Tick();
            Assert.That(Status("ReuseProtection").IsRouteSet, Is.False);
            Assert.That(Status().OverrunMode, Is.EqualTo(OverrunProtectionMode.Restricted));
        }

        [Test]
        public void DefinitionChangesAfterInitializationDoNotChangeActiveSettings()
        {
            Initialize();
            route.routeClearTrackCircuitIds.Add("Alternate");
            route.requiredTurnouts[0].requiredPosition = TrackSwitchPosition.Reverse;
            route.overrunProtection.releaseSeconds = 100f;
            SetOccupied("Alternate", true);
            Request();
            Tick();

            Assert.That(Status().PathEstablished, Is.True);
            PassMainRoute(100f);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
        }
    }

    public abstract class TrackStationInterlockingTestFixture
    {
        protected const string ArrivalRouteId = "Arrival";
        protected TrackStationInterlockingContext context;
        protected TrackStationInterlockingDefinition definition;
        protected TrackStationInterlockingRouteDefinition route;

        [SetUp]
        public void SetUp()
        {
            context = new TrackStationInterlockingContext();
            context.Input.hasCircuitSource = true;
            context.Input.hasConnectionSource = true;
            var circuitIds = new[] { "Entry", "Platform", "Approach", "MainLock", "ProtectionLock", "Alternate", "ProtectionReuse" };
            foreach (string circuitId in circuitIds)
            {
                SetOccupied(circuitId, false);
            }
            SetSwitch("Main", TrackSwitchPosition.Normal);
            SetSwitch("Protection", TrackSwitchPosition.Normal);

            route = new TrackStationInterlockingRouteDefinition
            {
                routeId = ArrivalRouteId,
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Platform",
                routeClearTrackCircuitIds = new List<string> { "Entry", "Platform" },
                routeReleaseTrackCircuitIds = new List<string> { "Entry" },
                requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Main") },
                approachLock = new ApproachLockDefinition
                {
                    trackCircuitIds = new List<string> { "Approach" },
                    releaseSeconds = 5f
                },
                overrunProtection = new TrackStationInterlockingOverrunProtectionDefinition
                {
                    isEnabled = true,
                    turnoutRequirements = new List<TurnoutRequirement> { RequireTurnout("Protection") },
                    releaseSeconds = 10f
                }
            };
            definition = new TrackStationInterlockingDefinition
            {
                interlockingId = "Station",
                memberTrackCircuitIds = new List<string>(circuitIds),
                memberConnectionIds = new List<string> { "Main", "Protection" },
                routes = new List<TrackStationInterlockingRouteDefinition>
                {
                    route,
                    new()
                    {
                        routeId = "ReuseMain",
                        startTrackCircuitId = "Entry",
                        destinationTrackCircuitId = "Alternate",
                        routeClearTrackCircuitIds = new List<string> { "Entry", "Alternate" },
                        routeReleaseTrackCircuitIds = new List<string> { "Entry" },
                        requiredTurnouts = new List<TurnoutRequirement>
                        {
                            RequireTurnout("Main", TrackSwitchPosition.Reverse)
                        }
                    },
                    new()
                    {
                        routeId = "ReuseProtection",
                        startTrackCircuitId = "ProtectionReuse",
                        destinationTrackCircuitId = "ProtectionReuse",
                        routeClearTrackCircuitIds = new List<string> { "ProtectionReuse" },
                        routeReleaseTrackCircuitIds = new List<string> { "ProtectionReuse" },
                        requiredTurnouts = new List<TurnoutRequirement>
                        {
                            RequireTurnout("Protection", TrackSwitchPosition.Reverse)
                        }
                    }
                },
                turnoutLocks = new List<TrackStationInterlockingTurnoutLockDefinition>
                {
                    new() { connectionId = "Main", trackCircuitIds = new List<string> { "MainLock" } },
                    new() { connectionId = "Protection", trackCircuitIds = new List<string> { "ProtectionLock" } }
                }
            };
        }

        protected void Initialize()
        {
            Assert.That(TrackStationInterlockingLogic.TryInitialize(context, definition, out string error), Is.True, error);
        }

        protected static InterlockingRouteRequest CreateRequest(string routeId = ArrivalRouteId,
            InterlockingRouteOperation operation = InterlockingRouteOperation.Set)
        {
            return new InterlockingRouteRequest
            {
                RequestId = "Request",
                StationId = "Station",
                RouteId = routeId,
                Operation = operation
            };
        }

        protected InterlockingRouteRequestResult Request(string routeId = ArrivalRouteId)
        {
            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest(routeId));
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.None));
            return result;
        }

        protected InterlockingRouteRequestResult Cancel(string routeId = ArrivalRouteId)
        {
            var result = TrackStationInterlockingLogic.TryCancelRoute(context,
                CreateRequest(routeId, InterlockingRouteOperation.Cancel));
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.None));
            return result;
        }

        protected TrackStationInterlockingRouteStatus Status(string routeId = ArrivalRouteId)
        {
            Assert.That(TrackStationInterlockingLogic.TryGetRouteStatus(context, routeId, out var status), Is.True, routeId);
            return status;
        }

        protected void Tick(float deltaTimeSeconds = 0f)
        {
            TrackStationInterlockingLogic.Calculate(context, deltaTimeSeconds);
        }

        protected void PassMainRoute(float releaseTickSeconds = 0f)
        {
            SetOccupied("Entry", true);
            SetOccupied("Platform", true);
            Tick();
            SetOccupied("Entry", false);
            Tick(releaseTickSeconds);
        }

        protected void SetOccupied(string circuitId, bool occupied)
        {
            context.Input.OccupiedByCircuitId[circuitId] = occupied;
        }

        protected void SetSwitch(string connectionId, TrackSwitchPosition position, bool moving = false)
        {
            context.Input.ConnectionsById[connectionId] = new TrackStationInterlockingTurnoutInput(position, moving);
        }

        private static TurnoutRequirement RequireTurnout(string connectionId,
            TrackSwitchPosition position = TrackSwitchPosition.Normal)
        {
            return new TurnoutRequirement { connectionId = connectionId, requiredPosition = position };
        }
    }
}
