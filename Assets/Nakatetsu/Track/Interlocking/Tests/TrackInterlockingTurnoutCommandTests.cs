using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingTurnoutCommandTests : TrackInterlockingTestFixture
    {
        [Test]
        public void TurnoutCommandsWaitForTickAndEndWhenRequestedPositionsAreConfirmed()
        {
            SetSwitch("Main", TrackSwitchPosition.Reverse);
            SetSwitch("Protection", TrackSwitchPosition.Reverse);
            Initialize();
            Request();
            Assert.That(context.Output.TurnoutCommands, Is.Empty);

            Tick();
            Assert.That(context.Output.TurnoutCommands.Count, Is.EqualTo(2));
            Assert.That(Status().PathEstablished, Is.False);
            Assert.That(Status().ProceedAllowed, Is.False);

            SetSwitch("Main", TrackSwitchPosition.Normal);
            SetSwitch("Protection", TrackSwitchPosition.Normal);
            Tick();
            Assert.That(context.Output.TurnoutCommands, Is.Empty);
            Assert.That(Status().PathEstablished, Is.True);
            Assert.That(Status().ProceedAllowed, Is.True);
        }

        [Test]
        public void OccupiedOrMissingFoulingCircuitBlocksTurnoutCommandsAndFreshApplyGate()
        {
            SetSwitch("Main", TrackSwitchPosition.Reverse);
            SetOccupied("MainLock", true);
            Initialize();
            Request();
            Tick();
            Assert.That(context.Output.TurnoutCommands, Is.Empty);
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.False);

            SetOccupied("MainLock", false);
            Tick();
            Assert.That(context.Output.TurnoutCommands.Count, Is.EqualTo(1));
            context.Input.OccupiedByCircuitId.Remove("MainLock");
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.False);
            Tick();
            Assert.That(context.Output.TurnoutCommands, Is.Empty);

            SetOccupied("MainLock", false);
            Tick();
            Assert.That(context.Output.TurnoutCommands.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OtherRequiredTurnoutInputLossBlocksAlreadyPublishedCommand(bool otherIsMainTurnout)
        {
            if (otherIsMainTurnout)
            {
                route.overrunProtection.isEnabled = false;
                route.requiredTurnouts.Add(new TurnoutRequirement
                {
                    connectionId = "Protection", requiredPosition = TrackSwitchPosition.Normal
                });
            }
            SetSwitch("Main", TrackSwitchPosition.Reverse);
            Initialize();
            Request();
            Tick();
            var published = context.Output;
            Assert.That(published.TurnoutCommands.Count, Is.EqualTo(1));
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.True);

            context.Input.ConnectionsById.Remove("Protection");
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.False);
            SetSwitch("Protection", TrackSwitchPosition.Unknown);
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.False);
            Assert.That(context.Output, Is.SameAs(published));
            Assert.That(published.TurnoutCommands.Count, Is.EqualTo(1));

            SetSwitch("Protection", TrackSwitchPosition.Normal);
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EntryOrCancellationWithdrawsTurnoutCommands(bool cancel)
        {
            SetSwitch("Main", TrackSwitchPosition.Reverse);
            Initialize();
            Request();
            Tick();
            Assert.That(context.Output.TurnoutCommands.Count, Is.EqualTo(1));

            if (cancel)
            {
                Cancel();
            }
            else
            {
                SetOccupied("Entry", true);
            }
            Assert.That(TrackInterlockingLogic.CanRequestTurnoutPosition(context, ArrivalRouteId, "Main"), Is.False);
            Tick();
            Assert.That(context.Output.TurnoutCommands, Is.Empty);
            Assert.That(Status().ProceedAllowed, Is.False);
        }

        [Test]
        public void SharedMainAndProtectionTurnoutHasOneCommandAndRemainsHeldAfterMainRelease()
        {
            route.overrunProtection.turnoutRequirements = new List<TurnoutRequirement>
            {
                new() { connectionId = "Main", requiredPosition = TrackSwitchPosition.Normal }
            };
            SetSwitch("Main", TrackSwitchPosition.Reverse);
            Initialize();
            Request();
            Tick();
            Assert.That(context.Output.TurnoutCommands.Count, Is.EqualTo(1));

            SetSwitch("Main", TrackSwitchPosition.Normal);
            Tick();
            PassMainRoute();
            Assert.That(Status().RouteLocked, Is.False);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, "ReuseMain", out _), Is.False);

            Tick(10f);
            Assert.That(Status().IsRouteSet, Is.False);
            Request("ReuseMain");
        }

        [Test]
        public void InvalidTickTimeDoesNotObservePassageReleaseMainOrAdvanceProtectionTimer()
        {
            Initialize();
            Request();
            Tick();
            SetOccupied("Entry", true);
            Tick(float.NaN);
            var unavailable = context.Output.RoutesById[ArrivalRouteId];
            Assert.That(unavailable.IsAvailable, Is.False);
            Assert.That(unavailable.RouteLocked, Is.True);
            Assert.That(unavailable.CircuitPassageById["Entry"],
                Is.EqualTo(TrackInterlockingCircuitPassageState.NotEntered));
            Assert.That(context.Output.TurnoutCommands, Is.Empty);

            Tick();
            SetOccupied("Entry", false);
            Tick(float.PositiveInfinity);
            unavailable = context.Output.RoutesById[ArrivalRouteId];
            Assert.That(unavailable.RouteLocked, Is.True);
            Assert.That(unavailable.CircuitPassageById["Entry"],
                Is.EqualTo(TrackInterlockingCircuitPassageState.Occupied));

            Tick();
            Assert.That(Status().RouteLocked, Is.False);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(-1f);
            Tick(float.PositiveInfinity);
            Assert.That(context.Output.RoutesById[ArrivalRouteId].OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(1f);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(9f));
        }
    }
}
