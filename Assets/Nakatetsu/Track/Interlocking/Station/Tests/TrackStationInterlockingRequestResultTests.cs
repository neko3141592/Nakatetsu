using System.Collections.Generic;
using Nakatetsu.Contracts.Interlocking;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackStationInterlockingRequestResultTests : TrackStationInterlockingTestFixture
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AcceptedOperationsCopyRequestIdentity(bool cancel)
        {
            Initialize();
            if (cancel)
            {
                Request();
            }
            var request = CreateRequest(operation: cancel
                ? InterlockingRouteOperation.Cancel : InterlockingRouteOperation.Set);
            request.RequestId = "CorrelatedRequest";
            request.StationId = "RegisteredStation";

            var result = Submit(request, cancel);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.None));
            Assert.That(result.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(result.StationId, Is.EqualTo(request.StationId));
            Assert.That(result.RouteId, Is.EqualTo(request.RouteId));
            Assert.That(result.RelatedRouteId, Is.Null);
            Assert.That(result.RelatedCircuitId, Is.Null);
            Assert.That(result.RelatedTurnoutId, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullRequestIsInvalidForEitherOperation(bool cancel)
        {
            Initialize();

            AssertRejected(Submit(null, cancel), InterlockingRouteErrorCode.InvalidRequest);
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [TestCase(false, null)]
        [TestCase(false, "")]
        [TestCase(false, " \t")]
        [TestCase(true, null)]
        [TestCase(true, "")]
        [TestCase(true, " \t")]
        public void MissingRouteIdIsInvalidForEitherOperation(bool cancel, string routeId)
        {
            Initialize();
            var request = CreateRequest(routeId, cancel
                ? InterlockingRouteOperation.Cancel : InterlockingRouteOperation.Set);

            AssertRejected(Submit(request, cancel), InterlockingRouteErrorCode.InvalidRequest);
            Assert.That(Status().IsRouteSet, Is.False);
        }

        [TestCase(false, InterlockingRouteOperation.None)]
        [TestCase(false, InterlockingRouteOperation.Cancel)]
        [TestCase(true, InterlockingRouteOperation.None)]
        [TestCase(true, InterlockingRouteOperation.Set)]
        public void IncorrectOperationCannotSetOrCancelRoute(bool cancel, InterlockingRouteOperation operation)
        {
            Initialize();
            if (cancel)
            {
                Request();
            }

            AssertRejected(Submit(CreateRequest(operation: operation), cancel),
                InterlockingRouteErrorCode.InvalidRequest);
            Assert.That(Status().IsRouteSet, Is.EqualTo(cancel));
            Assert.That(Status().CancelPending, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UninitializedStationRejectsEitherOperation(bool cancel)
        {
            var request = CreateRequest(operation: cancel
                ? InterlockingRouteOperation.Cancel : InterlockingRouteOperation.Set);

            var result = Submit(request, cancel);

            AssertRejected(result, InterlockingRouteErrorCode.NotInitialized);
            Assert.That(result.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(result.StationId, Is.EqualTo(request.StationId));
            Assert.That(result.RouteId, Is.EqualTo(request.RouteId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UndefinedRouteIsDistinctFromUnsetCancellation(bool cancel)
        {
            Initialize();
            var request = CreateRequest("Unknown", cancel
                ? InterlockingRouteOperation.Cancel : InterlockingRouteOperation.Set);

            var result = Submit(request, cancel);

            AssertRejected(result, InterlockingRouteErrorCode.UnknownRoute);
            Assert.That(result.RouteId, Is.EqualTo("Unknown"));
        }

        [Test]
        public void DuplicateSettingReportsAlreadySetAndWithdrawsProceedWithoutAdvancingPassage()
        {
            Initialize();
            Request();
            Tick();
            var previousOutput = context.Output;
            SetOccupied("Entry", true);

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest());

            AssertRejected(result, InterlockingRouteErrorCode.AlreadySet);
            Assert.That(Status().RouteLocked, Is.True);
            Assert.That(Status().ProceedAllowed, Is.False);
            Assert.That(Status().CircuitPassageById["Entry"],
                Is.EqualTo(TrackStationInterlockingCircuitPassageState.NotEntered));
            Assert.That(previousOutput.RoutesById[ArrivalRouteId].ProceedAllowed, Is.True);
        }

        [Test]
        public void CancellingKnownUnsetRouteReportsNotSetBeforeInputAvailability()
        {
            Initialize();
            context.Input.hasCircuitSource = false;

            var result = TrackStationInterlockingLogic.TryCancelRoute(context,
                CreateRequest(operation: InterlockingRouteOperation.Cancel));

            AssertRejected(result, InterlockingRouteErrorCode.NotSet);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingEquipmentInputRejectsSettingAsUnavailable(bool circuit)
        {
            Initialize();
            if (circuit)
            {
                context.Input.OccupiedByCircuitId.Remove("Platform");
            }
            else
            {
                context.Input.ConnectionsById.Remove("Protection");
            }

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest());

            AssertRejected(result, InterlockingRouteErrorCode.InputUnavailable);
            if (circuit)
            {
                Assert.That(result.RelatedCircuitId, Is.EqualTo("Platform"));
            }
            else
            {
                Assert.That(result.RelatedTurnoutId, Is.EqualTo("Protection"));
            }
            Assert.That(context.Output.RoutesById[ArrivalRouteId].IsRouteSet, Is.False);
        }

        [Test]
        public void MissingCircuitSourceRejectsCancellationButKeepsLockAndWithdrawsProceed()
        {
            Initialize();
            Request();
            Tick();
            var previousOutput = context.Output;
            context.Input.hasCircuitSource = false;

            var result = TrackStationInterlockingLogic.TryCancelRoute(context,
                CreateRequest(operation: InterlockingRouteOperation.Cancel));

            AssertRejected(result, InterlockingRouteErrorCode.InputUnavailable);
            var status = context.Output.RoutesById[ArrivalRouteId];
            Assert.That(status.IsRouteSet, Is.True);
            Assert.That(status.RouteLocked, Is.True);
            Assert.That(status.CancelPending, Is.False);
            Assert.That(status.ProceedAllowed, Is.False);
            Assert.That(previousOutput.RoutesById[ArrivalRouteId].ProceedAllowed, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExplicitConflictInEitherDirectionReportsHoldingRouteBeforeTurnoutConflict(bool requestedRouteDefinesConflict)
        {
            var other = definition.routes[1];
            other.startTrackCircuitId = "Alternate";
            other.routeClearTrackCircuitIds = new List<string> { "Alternate" };
            other.routeReleaseTrackCircuitIds = new List<string> { "Alternate" };
            if (requestedRouteDefinesConflict)
            {
                other.conflictRouteIds.Add(ArrivalRouteId);
            }
            else
            {
                route.conflictRouteIds.Add(other.routeId);
            }
            Initialize();
            Request();

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, CreateRequest(other.routeId));

            AssertRejected(result, InterlockingRouteErrorCode.RouteConflict);
            Assert.That(result.RelatedRouteId, Is.EqualTo(ArrivalRouteId));
            Assert.That(Status(other.routeId).IsRouteSet, Is.False);
            Assert.That(Status().RouteLocked, Is.True);
        }

        [Test]
        public void StationDoesNotValidateManagementEnvelopeIds()
        {
            Initialize();
            var request = CreateRequest();
            request.RequestId = string.Empty;
            request.StationId = string.Empty;

            var result = TrackStationInterlockingLogic.TryRequestRoute(context, request);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.ErrorCode, Is.EqualTo(InterlockingRouteErrorCode.None));
            Assert.That(result.RequestId, Is.Empty);
            Assert.That(result.StationId, Is.Empty);
            Assert.That(Status().IsRouteSet, Is.True);
        }

        private InterlockingRouteRequestResult Submit(InterlockingRouteRequest request, bool cancel)
        {
            return cancel
                ? TrackStationInterlockingLogic.TryCancelRoute(context, request)
                : TrackStationInterlockingLogic.TryRequestRoute(context, request);
        }

        private static void AssertRejected(InterlockingRouteRequestResult result, InterlockingRouteErrorCode code)
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(code));
        }
    }
}
