using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Application.Simulation.Tests
{
    public sealed class ApplicationInterlockingSimulationTests
    {
        private const string StationId = "Station";
        private const string RouteId = "Route";
        private const float TickDurationSeconds = 0.015f;
        private readonly List<Object> createdObjects = new();
        private ApplicationSimulationController application;
        private TrackInterlockingManagementController manager;
        private TrackStationInterlockingController station;

        [SetUp]
        public void SetUp()
        {
            Assert.That(UnityEngine.Application.isPlaying, Is.True);
            var asset = ScriptableObject.CreateInstance<TrackStationInterlockingAsset>();
            createdObjects.Add(asset);
            asset.Definition.interlockingId = StationId;
            asset.Definition.memberTrackCircuitIds.Add("Entry");
            asset.Definition.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = RouteId,
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Entry",
                routeClearTrackCircuitIds = new List<string> { "Entry" },
                routeReleaseTrackCircuitIds = new List<string> { "Entry" }
            });

            var stationObject = CreateInactiveGameObject("Station");
            station = stationObject.AddComponent<TrackStationInterlockingController>();
            SetField(station, "interlockingAsset", asset);
            stationObject.SetActive(true);
            Assert.That(station.IsInitialized, Is.True);

            var registration = new TrackInterlockingManagementStationRegistration();
            SetField(registration, "stationId", StationId);
            SetField(registration, "interlocking", station);
            var managerObject = CreateInactiveGameObject("Management");
            manager = managerObject.AddComponent<TrackInterlockingManagementController>();
            SetField(manager, "stations", new List<TrackInterlockingManagementStationRegistration> { registration });
            managerObject.SetActive(true);
            Assert.That(manager.IsInitialized, Is.True);

            var applicationObject = CreateInactiveGameObject("Application");
            application = applicationObject.AddComponent<ApplicationSimulationController>();
            SetField(application, "tickDurationSeconds", TickDurationSeconds);
            SetField(application, "isPaused", true);
            SetField(application, "interlockingManagement", manager);
            applicationObject.SetActive(true);
            Assert.That(application.IsPaused, Is.True);
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
        public void StepOnceUpdatesStationOnceAndPublishesTheLatestStateThroughManagement()
        {
            // Publish an available input sample first so collecting before the station tick is observable.
            station.Context.Input.hasCircuitSource = true;
            station.Context.Input.hasConnectionSource = true;
            station.Context.Input.OccupiedByCircuitId["Entry"] = false;
            TrackStationInterlockingLogic.Calculate(station.Context, 0f);
            Assert.That(station.Context.Output.RoutesById[RouteId].IsAvailable, Is.True);
            long stationRevision = station.Context.State.stateRevision;

            application.StepOnce();

            Assert.That(application.CompletedTickCount, Is.EqualTo(1));
            Assert.That(application.ElapsedTimeSeconds, Is.EqualTo((double)TickDurationSeconds));
            Assert.That(station.Context.State.stateRevision, Is.EqualTo(stationRevision + 1));
            Assert.That(manager.Context.Output.StationsById.Keys, Is.EquivalentTo(new[] { StationId }));
            var snapshotRoute = manager.Context.Output.StationsById[StationId].RoutesById[RouteId];
            Assert.That(station.Context.Output.RoutesById[RouteId].IsAvailable, Is.False);
            Assert.That(snapshotRoute.IsAvailable, Is.False);
        }

        [TestCase("station_uninitialized")]
        [TestCase("manager_uninitialized")]
        [TestCase("manager_disabled")]
        public void StepOnceContinuesWorldTickWhenInterlockingCannotAdvanceAndWithdrawsOldSnapshot(string failure)
        {
            application.StepOnce();
            Assert.That(manager.Context.Output.StationsById.ContainsKey(StationId), Is.True);
            var previousSnapshot = manager.Context.Output;
            long stationRevision = station.Context.State.stateRevision;
            long completedTicks = application.CompletedTickCount;
            switch (failure)
            {
                case "station_uninitialized":
                    SetField(station.Context.State, "isInitialized", false);
                    break;
                case "manager_uninitialized":
                    manager.Context.State.IsInitialized = false;
                    break;
                case "manager_disabled":
                    manager.enabled = false;
                    Assert.That(manager.Context.Output.StationsById, Is.Empty);
                    break;
            }

            application.StepOnce();

            Assert.That(application.CompletedTickCount, Is.EqualTo(completedTicks + 1));
            Assert.That(application.ElapsedTimeSeconds,
                Is.EqualTo((completedTicks + 1) * (double)TickDurationSeconds));
            Assert.That(station.Context.State.stateRevision, Is.EqualTo(stationRevision));
            Assert.That(manager.Context.Output.StationsById, Is.Empty);
            Assert.That(manager.Context.Output, Is.Not.SameAs(previousSnapshot));
            Assert.That(previousSnapshot.StationsById.ContainsKey(StationId), Is.True);
        }

        private GameObject CreateInactiveGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }
    }
}
