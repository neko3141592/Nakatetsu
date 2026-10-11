using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEngine;
using OverrunProtectionMode = Nakatetsu.Track.Simulation.Circuit.OverrunProtectionMode;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackAtcManagementTests
    {
        private readonly List<Object> createdObjects = new();
        private TrackInterlockingManagementController manager;
        private TrackAtcController atc;
        private TrackCircuitSimulationController circuits;
        private TrackInterlockingManagementStationInput first;
        private TrackInterlockingManagementStationInput second;

        [SetUp]
        public void SetUp()
        {
            manager = CreateInactiveObject("Management").AddComponent<TrackInterlockingManagementController>();
            manager.gameObject.SetActive(true);
            Assert.That(manager.TryInitialize(out string error), Is.True, error);

            circuits = CreateInactiveObject("Circuits").AddComponent<TrackCircuitSimulationController>();
            var occupancy = GetField<Dictionary<string, bool>>(circuits.Context.State, "occupiedByCircuitId");
            occupancy.Add("First", false);
            occupancy.Add("Second", false);

            var graph = ScriptableObject.CreateInstance<TrackAtcGraphAsset>();
            createdObjects.Add(graph);
            graph.SetCompiledDefinition(CreateGraph());
            var clock = CreateInactiveObject("Clock").AddComponent<TrackAtcManagementTestClock>();
            atc = CreateInactiveObject("Atc").AddComponent<TrackAtcController>();
            SetField(atc, "atcGraphAsset", graph);
            SetField(atc, "trackCircuitSimulation", circuits);
            SetField(atc, "interlockingManagement", manager);
            atc.SetWorldTimeSource(clock);

            first = ReadyStation("First");
            second = ReadyStation("Second");
            manager.Context.Input.StationsById.Add("FirstStation", first);
            manager.Context.Input.StationsById.Add("SecondStation", second);
            PublishSnapshot();
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

        [TestCase(false, false, false, false, false, OverrunProtectionMode.None)]
        [TestCase(true, true, true, true, false, OverrunProtectionMode.Normal)]
        [TestCase(true, false, true, true, false, OverrunProtectionMode.Restricted)]
        [TestCase(true, true, false, true, false, OverrunProtectionMode.None)]
        [TestCase(true, true, true, false, false, OverrunProtectionMode.Normal)]
        [TestCase(true, false, true, true, true, OverrunProtectionMode.Restricted)]
        public void CollectsAllAtcRouteFieldsFromThePublishedStationSnapshot(
            bool isSet, bool proceed, bool established, bool locked, bool cancelled, OverrunProtectionMode mode)
        {
            var route = first.RoutesById["FirstRoute"];
            route.IsRouteSet = isSet;
            route.ProceedAllowed = proceed;
            route.PathEstablished = established;
            route.RouteLocked = locked;
            route.CancelPending = cancelled;
            route.OverrunMode = mode;
            PublishSnapshot();

            // 未公開のInput変更は地上ATCへ伝わらない。
            route.IsRouteSet = !isSet;
            route.ProceedAllowed = !proceed;
            route.PathEstablished = !established;
            route.RouteLocked = !locked;
            route.CancelPending = !cancelled;
            route.OverrunMode = (OverrunProtectionMode)999;
            atc.Calculate(0f);

            var input = atc.Context.Input.RoutesById["FirstRoute"];
            Assert.That(input.IsRouteSet, Is.EqualTo(isSet));
            Assert.That(input.ProceedAllowed, Is.EqualTo(proceed));
            Assert.That(input.PathEstablished, Is.EqualTo(established));
            Assert.That(input.RouteLocked, Is.EqualTo(locked));
            Assert.That(input.CancelPending, Is.EqualTo(cancelled));
            Assert.That(input.OverrunMode, Is.EqualTo(mode));
            Assert.That(atc.Context.Input.RoutesById.ContainsKey("SecondRoute"), Is.True);
        }

        [Test]
        public void KnownUnsetRoutesInBothStationsProduceValidStopTelegrams()
        {
            atc.Calculate(0f);
            atc.ApplyOutput(0f);

            Assert.That(atc.Context.Input.RoutesById.Keys,
                Is.EquivalentTo(new[] { "FirstRoute", "SecondRoute" }));
            AssertTelegram("First", OverrunProtectionMode.None);
            AssertTelegram("Second", OverrunProtectionMode.None);
        }

        [Test]
        public void BothStationsSupplyTheirOwnProtectionModesAndFreshTelegrams()
        {
            SetActiveRoute(first, "FirstRoute", OverrunProtectionMode.Restricted);
            SetActiveRoute(second, "SecondRoute", OverrunProtectionMode.Normal);
            PublishSnapshot();
            atc.Calculate(0f);
            atc.ApplyOutput(0f);

            AssertTelegram("First", OverrunProtectionMode.Restricted);
            AssertTelegram("Second", OverrunProtectionMode.Normal);

            first.RoutesById["FirstRoute"].CancelPending = true;
            PublishSnapshot();
            atc.Calculate(0f);
            atc.ApplyOutput(0f);

            AssertTelegram("First", OverrunProtectionMode.None);
            AssertTelegram("Second", OverrunProtectionMode.Normal);
        }

        [TestCase("uninitialized")]
        [TestCase("output_missing")]
        [TestCase("station_missing")]
        [TestCase("route_unavailable")]
        [TestCase("route_missing")]
        [TestCase("route_null")]
        public void MissingStationOrRouteWithdrawsItsOldInputAndInvalidatesOnlyItsTelegram(string failure)
        {
            atc.Calculate(0f);
            Assert.That(atc.Context.Input.RoutesById.ContainsKey("FirstRoute"), Is.True);
            switch (failure)
            {
                case "uninitialized":
                    first.IsInitialized = false;
                    break;
                case "output_missing":
                    first.HasOutput = false;
                    break;
                case "station_missing":
                    manager.Context.Input.StationsById.Remove("FirstStation");
                    break;
                case "route_unavailable":
                    first.RoutesById["FirstRoute"].IsAvailable = false;
                    break;
                case "route_missing":
                    first.RoutesById.Clear();
                    break;
                case "route_null":
                    first.RoutesById["FirstRoute"] = null;
                    break;
            }
            PublishSnapshot();
            atc.Calculate(0f);
            atc.ApplyOutput(0f);

            Assert.That(atc.Context.Input.RoutesById.Keys, Is.EquivalentTo(new[] { "SecondRoute" }));
            Assert.That(atc.Context.State.validation.hasRouteInputById["FirstRoute"], Is.False);
            Assert.That(atc.Context.Output.telegrams["First"].isValid, Is.False);
            AssertTelegram("Second", OverrunProtectionMode.None);
        }

        [TestCase("missing")]
        [TestCase("uninitialized")]
        [TestCase("disabled")]
        [TestCase("inactive")]
        public void UnavailableManagerCannotReusePreviouslyCollectedRoutes(string failure)
        {
            atc.Calculate(0f);
            Assert.That(atc.Context.Input.RoutesById.Count, Is.EqualTo(2));
            switch (failure)
            {
                case "missing":
                    SetField(atc, "interlockingManagement", null);
                    break;
                case "uninitialized":
                    manager.Context.State.IsInitialized = false;
                    break;
                case "disabled":
                    manager.enabled = false;
                    break;
                case "inactive":
                    manager.gameObject.SetActive(false);
                    break;
            }
            atc.Calculate(0f);
            atc.ApplyOutput(0f);

            Assert.That(atc.Context.Input.RoutesById, Is.Empty);
            Assert.That(atc.Context.Output.telegrams["First"].isValid, Is.False);
            Assert.That(atc.Context.Output.telegrams["Second"].isValid, Is.False);
        }

        [Test]
        public void StationChangesInTheCurrentStepReachAtcAfterManagementCalculateAndApplyOutput()
        {
            var connections = CreateInactiveObject("Connections").AddComponent<TrackConnectionController>();
            typeof(TrackConnectionContext).GetProperty("IsInitialized").SetValue(connections.Context, true);
            var firstStation = CreateStation("First", connections);
            var secondStation = CreateStation("Second", connections);
            SetField(manager, "stations", new List<TrackInterlockingManagementStationRegistration>
            {
                Register("FirstStation", firstStation), Register("SecondStation", secondStation)
            });
            Assert.That(manager.TryInitialize(out string error), Is.True, error);
            manager.Calculate(0f);
            manager.ApplyOutput(0f);
            atc.Calculate(0f);
            Assert.That(atc.Context.Input.RoutesById["FirstRoute"].IsRouteSet, Is.False);

            foreach (string id in new[] { "First", "Second" })
            {
                var result = manager.TryRouteRequest(new InterlockingRouteRequest
                {
                    RequestId = id, StationId = id + "Station", RouteId = id + "Route",
                    Operation = InterlockingRouteOperation.Set
                });
                Assert.That(result.Accepted, Is.True, result.Message);
            }
            long firstRevision = firstStation.Context.State.stateRevision;
            long secondRevision = secondStation.Context.State.stateRevision;

            manager.Calculate(0.02f);
            manager.ApplyOutput(0.02f);
            atc.Calculate(0.02f);
            atc.ApplyOutput(0.02f);

            Assert.That(firstStation.Context.State.stateRevision, Is.EqualTo(firstRevision + 1));
            Assert.That(secondStation.Context.State.stateRevision, Is.EqualTo(secondRevision + 1));
            foreach (string id in new[] { "First", "Second" })
            {
                var input = atc.Context.Input.RoutesById[id + "Route"];
                Assert.That(input.IsRouteSet, Is.True);
                Assert.That(input.ProceedAllowed, Is.True);
                Assert.That(input.PathEstablished, Is.True);
                Assert.That(input.RouteLocked, Is.True);
                AssertTelegram(id, OverrunProtectionMode.None);
            }
        }

        private void PublishSnapshot() => TrackInterlockingManagementLogic.Calculate(manager.Context);

        private void AssertTelegram(string id, OverrunProtectionMode mode)
        {
            var telegram = atc.Context.Output.telegrams[id];
            Assert.That(telegram.isValid, Is.True, id);
            Assert.That(telegram.issuedAtSeconds, Is.EqualTo(120d));
            Assert.That(telegram.routeAtoB.atcEdgePath, Is.EqualTo(new[] { id }));
            Assert.That(telegram.routeAtoB.stopAtcEdgeId, Is.EqualTo(id));
            Assert.That(telegram.routeAtoB.overrunProtectionMode, Is.EqualTo(mode));
            Assert.That(telegram.routeBtoA.atcEdgePath, Is.EqualTo(new[] { id }));
            Assert.That(telegram.routeBtoA.overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.None));
        }

        private static TrackInterlockingManagementStationInput ReadyStation(string id)
        {
            var station = new TrackInterlockingManagementStationInput { IsInitialized = true, HasOutput = true };
            station.RoutesById.Add(id + "Route", new TrackInterlockingManagementRouteInput { IsAvailable = true });
            return station;
        }

        private static void SetActiveRoute(TrackInterlockingManagementStationInput station,
            string routeId, OverrunProtectionMode mode)
        {
            station.RoutesById[routeId] = new TrackInterlockingManagementRouteInput
            {
                IsAvailable = true, IsRouteSet = true, ProceedAllowed = true,
                PathEstablished = true, RouteLocked = true, OverrunMode = mode
            };
        }

        private static TrackAtcGraphDefinition CreateGraph()
        {
            var graph = new TrackAtcGraphDefinition { atcGraphId = "TwoStations", maximumOperatingSpeedKmh = 120f };
            foreach (string id in new[] { "First", "Second" })
            {
                graph.atcEdge.Add(new TrackAtcGraphEdge
                {
                    atcEdgeId = id, trackCircuitId = id, lengthM = 100f,
                    atcNodeAId = id + "A", atcNodeBId = id + "B",
                    controlKind = TrackAtcEdgeControlKind.Interlocking, direction = TrackAtcTravelDirection.AtoB
                });
                graph.atcNode.Add(new TrackAtcGraphNode
                {
                    atcNodeId = id + "A", connectedAtcEdgeIds = new List<string> { id }
                });
                graph.atcNode.Add(new TrackAtcGraphNode
                {
                    atcNodeId = id + "B", connectedAtcEdgeIds = new List<string> { id }
                });
                graph.routes.Add(new TrackAtcRouteDefinition
                {
                    atcRouteId = id + "AtcRoute", interlockingRouteId = id + "Route",
                    entryDirection = TrackAtcTravelDirection.AtoB, atcEdgeIds = new List<string> { id }
                });
            }
            return graph;
        }

        private TrackStationInterlockingController CreateStation(string id, TrackConnectionController connections)
        {
            var asset = ScriptableObject.CreateInstance<TrackStationInterlockingAsset>();
            createdObjects.Add(asset);
            asset.Definition.interlockingId = id;
            asset.Definition.memberTrackCircuitIds.Add(id);
            asset.Definition.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = id + "Route", startTrackCircuitId = id, destinationTrackCircuitId = id,
                routeClearTrackCircuitIds = new List<string> { id },
                routeReleaseTrackCircuitIds = new List<string> { id }
            });
            var station = CreateInactiveObject(id).AddComponent<TrackStationInterlockingController>();
            SetField(station, "interlockingAsset", asset);
            SetField(station, "trackCircuitSimulation", circuits);
            SetField(station, "trackConnectionController", connections);
            station.gameObject.SetActive(true);
            Assert.That(station.TryInitialize(out string error), Is.True, error);
            return station;
        }

        private static TrackInterlockingManagementStationRegistration Register(
            string stationId, TrackStationInterlockingController station)
        {
            var registration = new TrackInterlockingManagementStationRegistration();
            SetField(registration, "stationId", stationId);
            SetField(registration, "interlocking", station);
            return registration;
        }

        private GameObject CreateInactiveObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static T GetField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }
    }

    public sealed class TrackAtcManagementTestClock : MonoBehaviour, IWorldTimeSource
    {
        public double WorldTimeSeconds => 120d;
    }
}
