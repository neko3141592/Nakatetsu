using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingManagementSimulationTests
    {
        private const string RouteId = "Route";
        private const string TurnoutId = "Point";
        private readonly List<Object> createdObjects = new();
        private TrackInterlockingManagementController manager;
        private StationFixture firstStation;
        private StationFixture secondStation;

        [SetUp]
        public void SetUp()
        {
            firstStation = CreateStation("FirstStation");
            secondStation = CreateStation("SecondStation");
            var gameObject = CreateInactiveGameObject("Management");
            manager = gameObject.AddComponent<TrackInterlockingManagementController>();
            SetPrivateField(manager, "stations", new List<TrackInterlockingManagementStationRegistration>
            {
                CreateRegistration(firstStation),
                CreateRegistration(secondStation)
            });
            gameObject.SetActive(true);
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
        public void CalculateAdvancesEachStationOnceAndPublishesTheSameTickCancellationTimers()
        {
            BeginCancellation(firstStation);
            BeginCancellation(secondStation);
            manager.Calculate(0f);
            var previousSnapshot = manager.Context.Output;
            long firstRevision = firstStation.Controller.Context.State.stateRevision;
            long secondRevision = secondStation.Controller.Context.State.stateRevision;

            manager.Calculate(0.75f);

            Assert.That(firstStation.Controller.Context.State.stateRevision, Is.EqualTo(firstRevision + 1));
            Assert.That(secondStation.Controller.Context.State.stateRevision, Is.EqualTo(secondRevision + 1));
            Assert.That(Status(firstStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4.25f));
            Assert.That(Status(secondStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4.25f));
            Assert.That(SnapshotStatus(firstStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4.25f));
            Assert.That(SnapshotStatus(secondStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4.25f));
            Assert.That(previousSnapshot.StationsById[firstStation.Id].RoutesById[RouteId]
                .ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
        }

        [TestCase("null")]
        [TestCase("destroyed")]
        [TestCase("disabled")]
        [TestCase("inactive")]
        [TestCase("uninitialized")]
        public void UnavailableChildIsSkippedAndWithdrawnWhileTheOtherStationContinues(string unavailableState)
        {
            BeginCancellation(firstStation);
            BeginCancellation(secondStation);
            manager.Calculate(0f);
            var previousSnapshot = manager.Context.Output;
            var firstContext = firstStation.Controller.Context;
            var firstOutput = firstContext.Output;
            long firstRevision = firstContext.State.stateRevision;
            long secondRevision = secondStation.Controller.Context.State.stateRevision;
            MakeStationUnavailable(firstStation, unavailableState);

            manager.Calculate(1f);

            Assert.That(manager.Context.Output.StationsById.Keys, Is.EquivalentTo(new[] { secondStation.Id }));
            Assert.That(SnapshotStatus(secondStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4f));
            Assert.That(secondStation.Controller.Context.State.stateRevision, Is.EqualTo(secondRevision + 1));
            Assert.That(firstContext.Output, Is.SameAs(firstOutput));
            Assert.That(firstContext.State.stateRevision, Is.EqualTo(firstRevision));
            Assert.That(firstContext.Output.RoutesById[RouteId].RouteLocked, Is.True);
            Assert.That(firstContext.Output.RoutesById[RouteId].ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
            Assert.That(previousSnapshot.StationsById.Keys,
                Is.EquivalentTo(new[] { firstStation.Id, secondStation.Id }));
        }

        [TestCase("disabled")]
        [TestCase("inactive")]
        [TestCase("uninitialized")]
        public void UnavailableManagerWithdrawsSnapshotWithoutAdvancingOrErasingStationLocks(string unavailableState)
        {
            BeginCancellation(firstStation);
            BeginCancellation(secondStation);
            manager.Calculate(0f);
            long firstRevision = firstStation.Controller.Context.State.stateRevision;
            long secondRevision = secondStation.Controller.Context.State.stateRevision;
            switch (unavailableState)
            {
                case "disabled":
                    DisableManager();
                    break;
                case "inactive":
                    DisableManager(deactivateGameObject: true);
                    break;
                case "uninitialized":
                    manager.Context.State.IsInitialized = false;
                    break;
            }
            if (unavailableState != "uninitialized")
            {
                Assert.That(manager.Context.Output.StationsById, Is.Empty,
                    "OnDisable must withdraw the snapshot before another tick runs.");
            }

            manager.Calculate(1f);
            manager.ApplyOutput(1f);

            Assert.That(manager.Context.Output.StationsById, Is.Empty);
            Assert.That(firstStation.Controller.Context.State.stateRevision, Is.EqualTo(firstRevision));
            Assert.That(secondStation.Controller.Context.State.stateRevision, Is.EqualTo(secondRevision));
            Assert.That(Status(firstStation).ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
            Assert.That(Status(secondStation).ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
            Assert.That(Status(firstStation).RouteLocked, Is.True);
            Assert.That(Status(secondStation).RouteLocked, Is.True);
        }

        [Test]
        public void ApplyOutputWithoutManagerCalculationDoesNotSendOldStationCommands()
        {
            Request(firstStation, InterlockingRouteOperation.Set);
            firstStation.Controller.Calculate(0f);
            Assert.That(firstStation.Controller.Context.Output.TurnoutCommands.Count, Is.EqualTo(1));

            manager.ApplyOutput(0f);

            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Normal);
        }

        [Test]
        public void ApplyOutputRunsOncePerCalculationAndFreshCalculationEnablesTheNextApply()
        {
            Request(firstStation, InterlockingRouteOperation.Set);
            manager.Calculate(0f);
            Assert.That(firstStation.Controller.Context.Output.TurnoutCommands.Count, Is.EqualTo(1));

            manager.ApplyOutput(0f);

            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Reverse);
            Assert.That(firstStation.Connections.TryRequestPosition(TurnoutId, TrackSwitchPosition.Normal,
                out string error), Is.True, error);
            manager.ApplyOutput(0f);
            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Normal);

            manager.Calculate(0f);
            manager.ApplyOutput(0f);
            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Reverse);
        }

        [TestCase("destroyed")]
        [TestCase("disabled")]
        [TestCase("inactive")]
        [TestCase("uninitialized")]
        public void ApplyOutputRechecksChildAvailabilityAfterCalculation(string unavailableState)
        {
            Request(firstStation, InterlockingRouteOperation.Set);
            manager.Calculate(0f);
            var firstContext = firstStation.Controller.Context;
            Assert.That(firstContext.Output.TurnoutCommands.Count, Is.EqualTo(1));
            MakeStationUnavailable(firstStation, unavailableState);

            Assert.DoesNotThrow(() => manager.ApplyOutput(0f));

            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Normal);
            Assert.That(firstContext.Output.RoutesById[RouteId].RouteLocked, Is.True);
        }

        [Test]
        public void DisablingManagerDiscardsPendingCommandsEvenAfterItIsEnabledAgain()
        {
            Request(firstStation, InterlockingRouteOperation.Set);
            manager.Calculate(0f);
            Assert.That(firstStation.Controller.Context.Output.TurnoutCommands.Count, Is.EqualTo(1));

            DisableManager();
            Assert.That(manager.Context.Output.StationsById, Is.Empty);
            manager.enabled = true;
            manager.ApplyOutput(0f);

            AssertTurnoutPosition(firstStation, TrackSwitchPosition.Normal);
            Assert.That(Status(firstStation).RouteLocked, Is.True);
        }

        [Test]
        public void FailedChildInitializationDoesNotStopOtherStationsOrPublishItsOldOutput()
        {
            firstStation.Controller.Context.State.isInitialized = false;
            firstStation.Asset.Definition.routes[0].routeId = string.Empty;
            Assert.That(firstStation.Controller.TryInitialize(out _), Is.False);
            BeginCancellation(secondStation);
            long firstRevision = firstStation.Controller.Context.State.stateRevision;
            long secondRevision = secondStation.Controller.Context.State.stateRevision;

            manager.Calculate(1f);

            Assert.That(firstStation.Controller.Context.State.stateRevision, Is.EqualTo(firstRevision));
            Assert.That(secondStation.Controller.Context.State.stateRevision, Is.EqualTo(secondRevision + 1));
            Assert.That(manager.Context.Output.StationsById.Keys, Is.EquivalentTo(new[] { secondStation.Id }));
            Assert.That(SnapshotStatus(secondStation).ApproachReleaseRemainingSeconds, Is.EqualTo(4f));
        }

        private StationFixture CreateStation(string stationId)
        {
            var circuits = CreateInactiveGameObject(stationId + "Circuits")
                .AddComponent<TrackCircuitSimulationController>();
            var occupancy = GetPrivateField<Dictionary<string, bool>>(circuits.Context.State, "occupiedByCircuitId");
            occupancy.Add("Entry", false);
            occupancy.Add("Approach", false);
            occupancy.Add("Lock", false);

            // The concrete source controllers supply station input without a graph or running equipment tick.
            var connections = CreateInactiveGameObject(stationId + "Connections")
                .AddComponent<TrackConnectionController>();
            var states = GetPrivateField<Dictionary<string, TrackConnectionState>>(connections.Context, "StatesById");
            var constructor = typeof(TrackConnectionState).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(TrackSwitchPosition), typeof(TrackSwitchPosition), typeof(bool) }, null);
            Assert.That(constructor, Is.Not.Null);
            states.Add(TurnoutId, (TrackConnectionState)constructor.Invoke(new object[]
            {
                TrackSwitchPosition.Normal, TrackSwitchPosition.Normal, false
            }));
            typeof(TrackConnectionContext).GetProperty("IsInitialized").SetValue(connections.Context, true);

            var asset = ScriptableObject.CreateInstance<TrackStationInterlockingAsset>();
            createdObjects.Add(asset);
            asset.Definition.interlockingId = stationId;
            asset.Definition.memberTrackCircuitIds.AddRange(new[] { "Entry", "Approach", "Lock" });
            asset.Definition.memberConnectionIds.Add(TurnoutId);
            asset.Definition.turnoutLocks.Add(new TrackStationInterlockingTurnoutLockDefinition
            {
                connectionId = TurnoutId,
                trackCircuitIds = new List<string> { "Lock" }
            });
            asset.Definition.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = RouteId,
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Entry",
                routeClearTrackCircuitIds = new List<string> { "Entry" },
                routeReleaseTrackCircuitIds = new List<string> { "Entry" },
                requiredTurnouts = new List<TurnoutRequirement>
                {
                    new() { connectionId = TurnoutId, requiredPosition = TrackSwitchPosition.Reverse }
                },
                approachLock = new ApproachLockDefinition
                {
                    trackCircuitIds = new List<string> { "Approach" },
                    releaseSeconds = 5f
                }
            });
            var gameObject = CreateInactiveGameObject(stationId);
            var station = gameObject.AddComponent<TrackStationInterlockingController>();
            SetPrivateField(station, "interlockingAsset", asset);
            SetPrivateField(station, "trackCircuitSimulation", circuits);
            SetPrivateField(station, "trackConnectionController", connections);
            gameObject.SetActive(true);
            Assert.That(station.TryInitialize(out string error), Is.True, error);
            return new StationFixture(stationId, station, asset, occupancy, connections);
        }

        private void BeginCancellation(StationFixture station)
        {
            Request(station, InterlockingRouteOperation.Set);
            station.Occupancy["Approach"] = true;
            Request(station, InterlockingRouteOperation.Cancel);
            Assert.That(Status(station).ApproachReleaseRemainingSeconds, Is.EqualTo(5f));
        }

        private void Request(StationFixture station, InterlockingRouteOperation operation)
        {
            var result = manager.TryRouteRequest(new InterlockingRouteRequest
            {
                RequestId = station.Id + operation,
                StationId = station.Id,
                RouteId = RouteId,
                Operation = operation
            });
            Assert.That(result.Accepted, Is.True, result.Message);
        }

        private void MakeStationUnavailable(StationFixture station, string unavailableState)
        {
            switch (unavailableState)
            {
                case "null":
                    GetPrivateField<Dictionary<string, TrackStationInterlockingController>>(manager,
                        "registeredStationsById")[station.Id] = null;
                    break;
                case "destroyed":
                    Object.DestroyImmediate(station.Controller.gameObject);
                    break;
                case "disabled":
                    station.Controller.enabled = false;
                    break;
                case "inactive":
                    station.Controller.gameObject.SetActive(false);
                    break;
                case "uninitialized":
                    station.Controller.Context.State.isInitialized = false;
                    break;
            }
        }

        private void DisableManager(bool deactivateGameObject = false)
        {
            if (deactivateGameObject)
            {
                manager.gameObject.SetActive(false);
            }
            else
            {
                manager.enabled = false;
            }

            // EditMode does not dispatch lifecycle callbacks for this runtime-only behaviour.
            // PlayMode covers Unity's dispatch; invoke the callback here to test its effects.
            if (!UnityEngine.Application.isPlaying)
            {
                var onDisable = typeof(TrackInterlockingManagementController).GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onDisable, Is.Not.Null);
                onDisable.Invoke(manager, null);
            }
        }

        private GameObject CreateInactiveGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static TrackInterlockingManagementStationRegistration CreateRegistration(StationFixture station)
        {
            var registration = new TrackInterlockingManagementStationRegistration();
            SetPrivateField(registration, "stationId", station.Id);
            SetPrivateField(registration, "interlocking", station.Controller);
            return registration;
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }

        private static TrackStationInterlockingRouteStatus Status(StationFixture station) =>
            station.Controller.Context.Output.RoutesById[RouteId];

        private TrackInterlockingManagementRouteStatus SnapshotStatus(StationFixture station) =>
            manager.Context.Output.StationsById[station.Id].RoutesById[RouteId];

        private static void AssertTurnoutPosition(StationFixture station, TrackSwitchPosition position)
        {
            Assert.That(station.Connections.TryGetState(TurnoutId, out var state), Is.True);
            Assert.That(state.RequestedPosition, Is.EqualTo(position));
            Assert.That(state.ActualPosition, Is.EqualTo(position));
            Assert.That(state.IsMoving, Is.False);
        }

        private sealed class StationFixture
        {
            public string Id { get; }
            public TrackStationInterlockingController Controller { get; }
            public TrackStationInterlockingAsset Asset { get; }
            public Dictionary<string, bool> Occupancy { get; }
            public TrackConnectionController Connections { get; }

            public StationFixture(string id, TrackStationInterlockingController controller,
                TrackStationInterlockingAsset asset, Dictionary<string, bool> occupancy,
                TrackConnectionController connections)
            {
                Id = id;
                Controller = controller;
                Asset = asset;
                Occupancy = occupancy;
                Connections = connections;
            }
        }
    }
}
