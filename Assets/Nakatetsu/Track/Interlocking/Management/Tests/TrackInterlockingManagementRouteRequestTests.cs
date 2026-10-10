using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingManagementRouteRequestTests
    {
        private const string FirstStationId = "FirstStation";
        private const string SecondStationId = "SecondStation";
        private const string RouteId = "Route";

        private readonly List<Object> createdObjects = new();
        private TrackInterlockingManagementController manager;
        private TrackStationInterlockingController firstStation;
        private TrackStationInterlockingController secondStation;

        [SetUp]
        public void SetUp()
        {
            firstStation = CreateStation(FirstStationId);
            secondStation = CreateStation(SecondStationId);
            manager = CreateInactiveGameObject("Management").AddComponent<TrackInterlockingManagementController>();
            SetPrivateField(manager, "stations", new List<TrackInterlockingManagementStationRegistration>
            {
                CreateRegistration(FirstStationId, firstStation),
                CreateRegistration(SecondStationId, secondStation)
            });
            Assert.That(manager.TryInitialize(out string error), Is.True, error);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
        }

        [Test]
        public void NullRequestIsInvalidBeforeManagerInitializationCheck()
        {
            manager.Context.State.IsInitialized = false;

            var result = manager.TryRouteRequest(null);

            AssertRejected(result, InterlockingRouteErrorCode.InvalidRequest, null);
        }

        [TestCase("RequestId", null)]
        [TestCase("RequestId", "")]
        [TestCase("RequestId", " \t")]
        [TestCase("StationId", null)]
        [TestCase("StationId", "")]
        [TestCase("StationId", " \t")]
        [TestCase("RouteId", null)]
        [TestCase("RouteId", "")]
        [TestCase("RouteId", " \t")]
        public void MissingRequiredIdIsInvalidBeforeManagerInitializationCheck(string field, string value)
        {
            manager.Context.State.IsInitialized = false;
            var request = CreateRequest();
            typeof(InterlockingRouteRequest).GetProperty(field).SetValue(request, value);

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.InvalidRequest, request);
        }

        [TestCase(InterlockingRouteOperation.None)]
        [TestCase((InterlockingRouteOperation)99)]
        public void UnsupportedOperationIsInvalidBeforeManagerInitializationCheck(InterlockingRouteOperation operation)
        {
            manager.Context.State.IsInitialized = false;
            var request = CreateRequest(operation: operation);

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.InvalidRequest, request);
        }

        [Test]
        public void UninitializedManagerRejectsValidRequestBeforeStationLookup()
        {
            manager.Context.State.IsInitialized = false;
            var request = CreateRequest("UnknownStation");

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.NotInitialized, request);
        }

        [Test]
        public void UnknownStationRejectsRequestWithoutChangingRegisteredStations()
        {
            var firstOutput = firstStation.Context.Output;
            var secondOutput = secondStation.Context.Output;
            var request = CreateRequest("UnknownStation");

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.UnknownStation, request);
            Assert.That(firstStation.Context.Output, Is.SameAs(firstOutput));
            Assert.That(secondStation.Context.Output, Is.SameAs(secondOutput));
        }

        [TestCase("null")]
        [TestCase("destroyed")]
        [TestCase("inactive")]
        [TestCase("disabled")]
        public void UnavailableRegisteredControllerRejectsRequestWithoutChangingOtherStation(string unavailableState)
        {
            switch (unavailableState)
            {
                case "null":
                    GetRegisteredStations()[FirstStationId] = null;
                    break;
                case "destroyed":
                    Object.DestroyImmediate(firstStation.gameObject);
                    break;
                case "inactive":
                    firstStation.gameObject.SetActive(false);
                    break;
                case "disabled":
                    firstStation.enabled = false;
                    break;
            }
            var otherOutput = secondStation.Context.Output;
            var request = CreateRequest();

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.InputUnavailable, request);
            Assert.That(secondStation.Context.Output, Is.SameAs(otherOutput));
        }

        [TestCase(InterlockingRouteOperation.Set)]
        [TestCase(InterlockingRouteOperation.Cancel)]
        public void UninitializedStationReturnsStationLogicError(InterlockingRouteOperation operation)
        {
            firstStation.Context.State.isInitialized = false;
            var request = CreateRequest(operation: operation);

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, InterlockingRouteErrorCode.NotInitialized, request);
        }

        [TestCase(InterlockingRouteOperation.Set)]
        [TestCase(InterlockingRouteOperation.Cancel)]
        public void RoutesOperationToRequestedStationAndPreservesRequestIds(InterlockingRouteOperation operation)
        {
            var input = firstStation.Context.Input;
            input.hasCircuitSource = true;
            input.hasConnectionSource = true;
            input.OccupiedByCircuitId["Entry"] = false;
            var seedResult = TrackStationInterlockingLogic.TryRequestRoute(firstStation.Context, CreateRequest());
            Assert.That(seedResult.Accepted, Is.True, seedResult.Message);

            var target = operation == InterlockingRouteOperation.Set ? firstStation : secondStation;
            var other = operation == InterlockingRouteOperation.Set ? secondStation : firstStation;
            var otherOutput = other.Context.Output;
            long otherRevision = other.Context.State.stateRevision;
            var request = CreateRequest(operation == InterlockingRouteOperation.Set ? FirstStationId : SecondStationId,
                operation);
            request.RequestId = "RoutingRequest";

            var result = manager.TryRouteRequest(request);

            AssertRejected(result, operation == InterlockingRouteOperation.Set
                ? InterlockingRouteErrorCode.AlreadySet : InterlockingRouteErrorCode.NotSet, request);
            Assert.That(target.Context.Output.RoutesById[RouteId].IsRouteSet,
                Is.EqualTo(operation == InterlockingRouteOperation.Set));
            Assert.That(other.Context.Output, Is.SameAs(otherOutput));
            Assert.That(other.Context.State.stateRevision, Is.EqualTo(otherRevision));
        }

        private TrackStationInterlockingController CreateStation(string stationId)
        {
            var asset = ScriptableObject.CreateInstance<TrackStationInterlockingAsset>();
            createdObjects.Add(asset);
            asset.Definition.interlockingId = stationId;
            asset.Definition.memberTrackCircuitIds.Add("Entry");
            asset.Definition.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = RouteId,
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Entry",
                routeClearTrackCircuitIds = new List<string> { "Entry" },
                routeReleaseTrackCircuitIds = new List<string> { "Entry" }
            });
            var gameObject = CreateInactiveGameObject(stationId);
            var station = gameObject.AddComponent<TrackStationInterlockingController>();
            SetPrivateField(station, "interlockingAsset", asset);
            gameObject.SetActive(true);
            Assert.That(station.TryInitialize(out string error), Is.True, error);
            return station;
        }

        private GameObject CreateInactiveGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static TrackInterlockingManagementStationRegistration CreateRegistration(string stationId,
            TrackStationInterlockingController station)
        {
            var registration = new TrackInterlockingManagementStationRegistration();
            SetPrivateField(registration, "stationId", stationId);
            SetPrivateField(registration, "interlocking", station);
            return registration;
        }

        private Dictionary<string, TrackStationInterlockingController> GetRegisteredStations()
        {
            var field = typeof(TrackInterlockingManagementController).GetField("registeredStationsById",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (Dictionary<string, TrackStationInterlockingController>)field.GetValue(manager);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }

        private static InterlockingRouteRequest CreateRequest(string stationId = FirstStationId,
            InterlockingRouteOperation operation = InterlockingRouteOperation.Set)
        {
            return new InterlockingRouteRequest
            {
                RequestId = "Request",
                StationId = stationId,
                RouteId = RouteId,
                Operation = operation
            };
        }

        private static void AssertRejected(InterlockingRouteRequestResult result, InterlockingRouteErrorCode errorCode,
            InterlockingRouteRequest request)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(errorCode));
            Assert.That(result.RequestId, Is.EqualTo(request?.RequestId ?? string.Empty));
            Assert.That(result.StationId, Is.EqualTo(request?.StationId ?? string.Empty));
            Assert.That(result.RouteId, Is.EqualTo(request?.RouteId ?? string.Empty));
        }
    }
}
