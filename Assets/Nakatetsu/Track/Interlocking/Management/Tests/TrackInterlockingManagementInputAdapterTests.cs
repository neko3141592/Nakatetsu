using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingManagementInputAdapterTests : TrackStationInterlockingTestFixture
    {
        [TestCase(true)]
        [TestCase(false)]
        public void CopyPublishedOutputDoesNotAdvanceStationStateOrReleaseTimers(bool overrunTimer)
        {
            Initialize();
            Request();
            Tick();
            if (overrunTimer)
            {
                PassMainRoute(2f);
                Assert.That(Status().OverrunReleaseRemainingSeconds, Is.GreaterThan(0f));
            }
            else
            {
                SetOccupied("Approach", true);
                Cancel();
                Tick(1f);
                Assert.That(Status().ApproachReleaseRemainingSeconds, Is.GreaterThan(0f));
            }

            var published = context.Output;
            long revision = context.State.stateRevision;
            bool healthy = context.State.isInterlockingHealthy;

            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(
                context.State.isInitialized, published);

            Assert.That(input.IsInitialized, Is.True);
            Assert.That(input.HasOutput, Is.True);
            Assert.That(input.RoutesById.Keys, Is.EquivalentTo(published.RoutesById.Keys));
            foreach (var pair in published.RoutesById)
            {
                AssertCopiedRoute(pair.Value, input.RoutesById[pair.Key]);
            }

            Assert.That(context.State.isInitialized, Is.True);
            Assert.That(context.State.isInterlockingHealthy, Is.EqualTo(healthy));
            Assert.That(context.State.stateRevision, Is.EqualTo(revision));
            Assert.That(context.Output, Is.SameAs(published));
            AssertCopiedRoute(Status(), input.RoutesById[ArrivalRouteId]);
        }

        [Test]
        public void StationOutputUpdatesDoNotChangePreviouslyCollectedInput()
        {
            Initialize();
            Request();
            Tick();
            PassMainRoute();
            Tick(2f);
            var published = context.Output;
            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(true, published);
            Assert.That(input.RoutesById[ArrivalRouteId].OverrunReleaseRemainingSeconds, Is.EqualTo(8f));

            Tick(1f);

            Assert.That(context.Output, Is.Not.SameAs(published));
            Assert.That(Status().OverrunReleaseRemainingSeconds, Is.EqualTo(7f));
            AssertCopiedRoute(published.RoutesById[ArrivalRouteId], input.RoutesById[ArrivalRouteId]);
            Assert.That(input.RoutesById[ArrivalRouteId].OverrunReleaseRemainingSeconds, Is.EqualTo(8f));
        }

        [Test]
        public void ChangingCollectedInputDoesNotChangeSourceStationOutput()
        {
            Initialize();
            Request();
            Tick();
            var published = context.Output;
            var sourceRoute = published.RoutesById[ArrivalRouteId];
            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(true, published);

            input.IsInitialized = false;
            input.HasOutput = false;
            var inputRoute = input.RoutesById[ArrivalRouteId];
            inputRoute.IsAvailable = false;
            inputRoute.IsRouteSet = false;
            inputRoute.PathEstablished = false;
            inputRoute.ProceedAllowed = false;
            inputRoute.RouteLocked = false;
            inputRoute.OverrunMode = OverrunProtectionMode.None;
            inputRoute.OverrunPhase = OverrunProtectionPhase.None;
            inputRoute.OverrunReleaseRemainingSeconds = 50f;
            input.RoutesById.Clear();

            Assert.That(context.State.isInitialized, Is.True);
            Assert.That(context.Output, Is.SameAs(published));
            Assert.That(published.RoutesById.Count, Is.EqualTo(3));
            Assert.That(published.RoutesById[ArrivalRouteId], Is.SameAs(sourceRoute));
            Assert.That(sourceRoute.IsAvailable, Is.True);
            Assert.That(sourceRoute.IsRouteSet, Is.True);
            Assert.That(sourceRoute.PathEstablished, Is.True);
            Assert.That(sourceRoute.ProceedAllowed, Is.True);
            Assert.That(sourceRoute.RouteLocked, Is.True);
            Assert.That(sourceRoute.OverrunMode, Is.EqualTo(OverrunProtectionMode.Normal));
            Assert.That(sourceRoute.OverrunPhase, Is.EqualTo(OverrunProtectionPhase.Established));
            Assert.That(sourceRoute.OverrunReleaseRemainingSeconds, Is.Zero);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void UninitializedOrMissingOutputProducesUnavailableStationWithoutRoutes(
            bool isInitialized, bool hasOutput)
        {
            Initialize();
            Assert.That(context.Output.RoutesById, Is.Not.Empty);

            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(
                isInitialized, hasOutput ? context.Output : null);

            Assert.That(input.IsInitialized, Is.EqualTo(isInitialized));
            Assert.That(input.HasOutput, Is.False);
            Assert.That(input.RoutesById, Is.Empty);
        }

        [Test]
        public void InitializedStationWithEmptyOutputIsSuccessfullyCollected()
        {
            definition.routes.Clear();
            Initialize();
            Assert.That(context.Output.RoutesById, Is.Empty);

            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(
                context.State.isInitialized, context.Output);

            Assert.That(input.IsInitialized, Is.True);
            Assert.That(input.HasOutput, Is.True);
            Assert.That(input.RoutesById, Is.Empty);
        }

        [Test]
        public void MissingEquipmentInputStillCopiesUnavailableRouteState()
        {
            Initialize();
            Request();
            Tick();
            context.Input.hasCircuitSource = false;
            Tick();
            Assert.That(TrackStationInterlockingLogic.TryGetRouteStatus(context, ArrivalRouteId, out _), Is.False);
            var published = context.Output;
            Assert.That(published.RoutesById[ArrivalRouteId].IsAvailable, Is.False);

            var input = TrackInterlockingManagementInputAdapter.CreateStationInput(
                context.State.isInitialized, published);

            Assert.That(input.HasOutput, Is.True);
            Assert.That(input.RoutesById.Keys, Is.EquivalentTo(published.RoutesById.Keys));
            var copiedRoute = input.RoutesById[ArrivalRouteId];
            Assert.That(copiedRoute.IsAvailable, Is.False);
            Assert.That(copiedRoute.IsRouteSet, Is.True);
            Assert.That(copiedRoute.RouteLocked, Is.True);
            AssertCopiedRoute(published.RoutesById[ArrivalRouteId], copiedRoute);
        }

        private static void AssertCopiedRoute(TrackStationInterlockingRouteStatus source,
            TrackInterlockingManagementRouteInput input)
        {
            Assert.That(input.IsAvailable, Is.EqualTo(source.IsAvailable));
            Assert.That(input.IsRouteSet, Is.EqualTo(source.IsRouteSet));
            Assert.That(input.PathEstablished, Is.EqualTo(source.PathEstablished));
            Assert.That(input.ProceedAllowed, Is.EqualTo(source.ProceedAllowed));
            Assert.That(input.RouteLocked, Is.EqualTo(source.RouteLocked));
            Assert.That(input.CancelPending, Is.EqualTo(source.CancelPending));
            Assert.That(input.ApproachLocked, Is.EqualTo(source.ApproachLocked));
            Assert.That(input.ApproachReleaseRemainingSeconds, Is.EqualTo(source.ApproachReleaseRemainingSeconds));
            Assert.That(input.OverrunMode, Is.EqualTo(source.OverrunMode));
            Assert.That(input.OverrunPhase, Is.EqualTo(source.OverrunPhase));
            Assert.That(input.OverrunReleaseRemainingSeconds, Is.EqualTo(source.OverrunReleaseRemainingSeconds));
        }
    }
}
