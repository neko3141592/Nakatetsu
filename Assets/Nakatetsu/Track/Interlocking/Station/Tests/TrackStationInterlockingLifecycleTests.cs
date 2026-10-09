using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackStationInterlockingLifecycleTests : TrackStationInterlockingTestFixture
    {
        [Test]
        public void MainRouteReleasesWithOccupiedPlatformWhileProtectionKeepsItsTurnout()
        {
            Initialize();
            Request();
            Tick();
            PassMainRoute(100f);

            var releasedMain = Status();
            Assert.That(releasedMain.IsRouteSet, Is.True);
            Assert.That(releasedMain.RouteLocked, Is.False);
            Assert.That(releasedMain.PathEstablished, Is.False);
            Assert.That(releasedMain.ProceedAllowed, Is.False);
            Assert.That(releasedMain.OverrunPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(releasedMain.OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Assert.That(context.Input.OccupiedByCircuitId["Platform"], Is.True);
            Assert.That(TrackStationInterlockingLogic.TryRequestRoute(context, "ReuseProtection", out _), Is.False);
            Request("ReuseMain");

            Tick(9f);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(1f));
            Tick(1f);
            Assert.That(Status().IsRouteSet, Is.False);
            Request("ReuseProtection");
        }

        [Test]
        public void ArrivalAloneDoesNotStartProtectionTimerAndReleaseStartsItOnlyOnce()
        {
            Initialize();
            Request();
            Tick();
            SetOccupied("Entry", true);
            SetOccupied("Platform", true);
            Tick(100f);

            Assert.That(Status().RouteLocked, Is.True);
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Established));
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.Zero);

            SetOccupied("Entry", false);
            Tick(100f);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(2f);
            SetOccupied("Platform", false);
            Cancel();
            Cancel();
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(8f));
            Tick(1f);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(7f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExpiredProtectionTimerWaitsForRequiredTurnoutPositionAndStopsWithoutRestart(bool moving)
        {
            Initialize();
            Request();
            Tick();
            PassMainRoute();
            SetSwitch("Protection", moving ? TrackSwitchPosition.Unknown : TrackSwitchPosition.Reverse, moving);
            Tick(10f);

            Assert.That(Status().IsRouteSet, Is.True);
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.Zero);

            SetSwitch("Protection", TrackSwitchPosition.Normal);
            Tick();
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void UnexpectedPassageStartsTimerForProtectionStillWaitingToEstablish()
        {
            SetSwitch("Protection", TrackSwitchPosition.Reverse);
            Initialize();
            Request();
            Tick();
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Setting));
            PassMainRoute(100f);

            Assert.That(Status().RouteLocked, Is.False);
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(10f);
            Assert.That(Status().IsRouteSet, Is.True);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.Zero);

            SetSwitch("Protection", TrackSwitchPosition.Normal);
            Tick();
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void OccupiedProtectionTurnoutCircuitDoesNotLatchAutomaticRelease()
        {
            Initialize();
            Request();
            Tick();
            PassMainRoute();
            SetOccupied("ProtectionLock", true);
            Tick(10f);

            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void RepeatedCancellationDoesNotRestartApproachTimerOrAdvanceTime()
        {
            Initialize();
            Request();
            Tick();
            SetOccupied("Approach", true);
            Cancel();
            Assert.That(Status().CancelPending, Is.True);
            Assert.That(Status().ProceedAllowed, Is.False);
            Assert.That(Status().ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
            Assert.That(context.Output.TurnoutCommands, Is.Empty);

            Tick(2f);
            Assert.That(Status().ApproachReleaseRemainingSeconds, Is.EqualTo(3f));
            context.Input.hasCircuitSource = false;
            Cancel();
            Assert.That(Status().ApproachReleaseRemainingSeconds, Is.EqualTo(3f));
            context.Input.hasCircuitSource = true;
            Tick(3f);
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [TestCase("Main")]
        [TestCase("Protection")]
        public void CancellationBeforeEntryWaitsForTurnoutToStopWithoutRequiringRequestedPosition(string connectionId)
        {
            SetSwitch(connectionId, TrackSwitchPosition.Unknown, true);
            Initialize();
            Request();
            Cancel();
            Tick(100f);
            Assert.That(Status().IsRouteSet, Is.True);
            Assert.That(context.Output.TurnoutCommands, Is.Empty);

            SetSwitch(connectionId, TrackSwitchPosition.Reverse);
            Tick();
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void CancellationAfterEntryKeepsMainLockUntilRearPassageThenStartsProtectionTimer()
        {
            Initialize();
            Request();
            Tick();
            SetOccupied("Entry", true);
            Tick();
            Cancel();
            Tick(100f);
            Assert.That(Status().RouteLocked, Is.True);
            Assert.That(Status().ProceedAllowed, Is.False);
            Assert.That(Status().OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Established));

            SetOccupied("Entry", false);
            Tick(100f);
            Assert.That(Status().RouteLocked, Is.False);
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(10f);
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [Test]
        public void MissingInputIsUnavailableButKnownUnsetRouteHasAvailableStatus()
        {
            Assert.That(TrackStationInterlockingLogic.TryGetRouteStatus(context, ArrivalRouteId, out _), Is.False);
            Initialize();
            Assert.That(Status().IsAvailable, Is.True);
            Assert.That(Status().IsRouteSet, Is.False);
            Assert.That(TrackStationInterlockingLogic.TryGetRouteStatus(context, "Unknown", out _), Is.False);

            Request();
            Tick();
            context.Input.OccupiedByCircuitId.Remove("Platform");
            Tick();
            Assert.That(TrackStationInterlockingLogic.TryGetRouteStatus(context, ArrivalRouteId, out _), Is.False);
            Assert.That(context.Output.RoutesById[ArrivalRouteId].ProceedAllowed, Is.False);

            SetOccupied("Platform", false);
            Tick();
            Assert.That(Status().IsRouteSet, Is.True);
        }

        [Test]
        public void OperationObservesOccupancyWithoutUpdatingPassageAndOldOutputRemainsUnchanged()
        {
            Initialize();
            Request();
            Tick();
            var previousOutput = context.Output;
            var previousStatus = Status();
            SetOccupied("Entry", true);
            Assert.That(TrackStationInterlockingLogic.TryRequestRoute(context, "ReuseProtection", out _), Is.False);

            Assert.That(Status().ProceedAllowed, Is.False);
            Assert.That(TrackStationInterlockingLogic.TryGetCircuitPassage(context, ArrivalRouteId, "Entry", out var passage), Is.True);
            Assert.That(passage, Is.EqualTo(TrackStationInterlockingCircuitPassageState.NotEntered));
            Assert.That(previousStatus.ProceedAllowed, Is.True);
            Assert.That(previousStatus.CircuitPassageById["Entry"], Is.EqualTo(TrackStationInterlockingCircuitPassageState.NotEntered));
            Assert.That(previousOutput.RoutesById["ReuseProtection"].IsRouteSet, Is.False);

            Tick();
            Assert.That(TrackStationInterlockingLogic.TryGetCircuitPassage(context, ArrivalRouteId, "Entry", out passage), Is.True);
            Assert.That(passage, Is.EqualTo(TrackStationInterlockingCircuitPassageState.Occupied));
            Assert.That(previousStatus.CircuitPassageById["Entry"], Is.EqualTo(TrackStationInterlockingCircuitPassageState.NotEntered));
        }

        [Test]
        public void ReinitializationCannotEraseHeldReservations()
        {
            Initialize();
            Request();

            Assert.That(TrackStationInterlockingLogic.TryInitialize(context, definition, out _), Is.False);
            Assert.That(Status().RouteLocked, Is.True);
            Assert.That(TrackStationInterlockingLogic.TryRequestRoute(context, "ReuseMain", out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledProtectionOrZeroDelayReleasesInSameTickAsMainRoute(bool protectionEnabled)
        {
            route.overrunProtection.isEnabled = protectionEnabled;
            route.overrunProtection.releaseSeconds = 0f;
            Initialize();
            Request();
            Tick();
            PassMainRoute();

            Assert.That(Status().IsRouteSet, Is.False);
        }
    }
}
