using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingReservationTests : TrackInterlockingTestFixture
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

            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, ArrivalRouteId, out _), Is.False);
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void MainCircuitReservationRejectsAnotherRouteWithoutPartialReservation()
        {
            Initialize();
            Request();

            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, "ReuseMain", out _), Is.False);
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

            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, "ReuseMain", out _), Is.EqualTo(accepted));
            Assert.That(Status("ReuseMain").IsRouteSet, Is.EqualTo(accepted));
            Assert.That(Status().RouteLocked, Is.True);
        }

        [Test]
        public void ProtectionTurnoutConflictSelectsRestrictedAndDoesNotUpgradeAutomatically()
        {
            Initialize();
            Request("ReuseProtection");
            Request();
            Tick();

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

    public abstract class TrackInterlockingTestFixture
    {
        protected const string ArrivalRouteId = "Arrival";
        protected TrackInterlockingContext context;
        protected TrackInterlockingDefinition definition;
        protected TrackInterlockingRouteDefinition route;

        [SetUp]
        public void SetUp()
        {
            context = new TrackInterlockingContext();
            context.Input.hasCircuitSource = true;
            context.Input.hasConnectionSource = true;
            var circuitIds = new[] { "Entry", "Platform", "Approach", "MainLock", "ProtectionLock", "Alternate", "ProtectionReuse" };
            foreach (string circuitId in circuitIds)
            {
                SetOccupied(circuitId, false);
            }
            SetSwitch("Main", TrackSwitchPosition.Normal);
            SetSwitch("Protection", TrackSwitchPosition.Normal);

            route = new TrackInterlockingRouteDefinition
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
                overrunProtection = new TrackInterlockingOverrunProtectionDefinition
                {
                    isEnabled = true,
                    turnoutRequirements = new List<TurnoutRequirement> { RequireTurnout("Protection") },
                    releaseSeconds = 10f
                }
            };
            definition = new TrackInterlockingDefinition
            {
                interlockingId = "Station",
                memberTrackCircuitIds = new List<string>(circuitIds),
                memberConnectionIds = new List<string> { "Main", "Protection" },
                routes = new List<TrackInterlockingRouteDefinition>
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
                turnoutLocks = new List<TrackInterlockingTurnoutLockDefinition>
                {
                    new() { connectionId = "Main", trackCircuitIds = new List<string> { "MainLock" } },
                    new() { connectionId = "Protection", trackCircuitIds = new List<string> { "ProtectionLock" } }
                }
            };
        }

        protected void Initialize()
        {
            Assert.That(TrackInterlockingLogic.TryInitialize(context, definition, out string error), Is.True, error);
        }

        protected void Request(string routeId = ArrivalRouteId)
        {
            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, routeId, out string error), Is.True, error);
        }

        protected void Cancel(string routeId = ArrivalRouteId)
        {
            Assert.That(TrackInterlockingLogic.TryCancelRoute(context, routeId, out string error), Is.True, error);
        }

        protected TrackInterlockingRouteStatus Status(string routeId = ArrivalRouteId)
        {
            Assert.That(TrackInterlockingLogic.TryGetRouteStatus(context, routeId, out var status), Is.True, routeId);
            return status;
        }

        protected void Tick(float deltaTimeSeconds = 0f)
        {
            TrackInterlockingLogic.Calculate(context, deltaTimeSeconds);
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
            context.Input.ConnectionsById[connectionId] = new TrackInterlockingTurnoutInput(position, moving);
        }

        private static TurnoutRequirement RequireTurnout(string connectionId,
            TrackSwitchPosition position = TrackSwitchPosition.Normal)
        {
            return new TurnoutRequirement { connectionId = connectionId, requiredPosition = position };
        }
    }
}
