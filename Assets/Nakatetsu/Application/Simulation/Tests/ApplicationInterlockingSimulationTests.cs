using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using Nakatetsu.Application.Communication;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Circuit;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Node;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEngine;
using Ctc = Namespace.Protocol.Ctc.V1;

namespace Nakatetsu.Application.Simulation.Tests
{
    public sealed class ApplicationInterlockingSimulationTests
    {
        private const string StationId = "Station";
        private const string RouteId = "Route";
        private const string ConnectionId = "Station:21";
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
        public void BeforeFirstTickPublishesEmptyInterlockingEvenWhenManagementHasOutput()
        {
            manager.Calculate(0f);
            manager.ApplyOutput(0f);
            Assert.That(manager.Context.Output.StationsById.ContainsKey(StationId), Is.True);

            Assert.That(application.CompletedTickCount, Is.Zero);
            Assert.That(application.ElapsedTimeSeconds, Is.Zero);
            Assert.That(application.TickDurationSeconds, Is.EqualTo(TickDurationSeconds));
            Assert.That(application.CompletedInterlockingOutput.StationsById, Is.Empty);
        }

        [Test]
        public void VariableTicksAccumulateTimeAndPublishTheLastCompletedDuration()
        {
            double initialWorldTime = application.WorldTimeSeconds;
            double elapsed = 0d;
            float[] durations = { 0.01f, 0.04f, 0.025f };
            for (int i = 0; i < durations.Length; i++)
            {
                ExecuteTick(durations[i]);
                elapsed += durations[i];
                Assert.That(application.CompletedTickCount, Is.EqualTo(i + 1));
                Assert.That(application.ElapsedTimeSeconds, Is.EqualTo(elapsed));
                Assert.That(application.WorldTimeSeconds, Is.EqualTo(initialWorldTime + elapsed));
                Assert.That(application.TickDurationSeconds, Is.EqualTo(durations[i]));
            }

            var snapshot = Ctc.UnitySnapshotMessage.Parser.ParseFrom(CreateSnapshot().ToByteArray());
            Assert.That(snapshot.Payload.WorldTime.TickCount, Is.EqualTo(3));
            Assert.That(snapshot.Payload.WorldTime.WorldTimeSeconds,
                Is.EqualTo((float)(initialWorldTime + elapsed)));
            Assert.That(snapshot.Payload.WorldTime.TickDurationSeconds, Is.EqualTo(durations[2]));

            SetField(application, "playbackSpeed", 10f);
            application.StepOnce();
            Assert.That(application.CompletedTickCount, Is.EqualTo(4));
            Assert.That(application.ElapsedTimeSeconds, Is.EqualTo(elapsed + TickDurationSeconds));
            Assert.That(application.TickDurationSeconds, Is.EqualTo(TickDurationSeconds));
            Assert.That(snapshot.Payload.WorldTime.TickCount, Is.EqualTo(3));
            Assert.That(snapshot.Payload.WorldTime.TickDurationSeconds, Is.EqualTo(durations[2]));
        }

        [Test]
        public void VariableTicksAdvanceTimedTurnoutByTheirActualDurations()
        {
            var connections = CreateConnections();
            SetField(connections, "completeRequestsAutomatically", true);
            SetField(connections, "switchDurationSeconds", 0.25f);
            Assert.That(connections.TryRequestPosition(ConnectionId, TrackSwitchPosition.Reverse,
                out string error), Is.True, error);

            ExecuteTick(0.0625f);
            ExecuteTick(0.125f);
            var moving = application.CompletedTurnoutsById;
            Assert.That(moving[ConnectionId].IsMoving, Is.True);
            Assert.That(moving[ConnectionId].ActualPosition, Is.EqualTo(TrackSwitchPosition.Unknown));

            ExecuteTick(0.0625f);
            var snapshot = CreateSnapshot();
            Assert.That(application.ElapsedTimeSeconds, Is.EqualTo(0.25d));
            Assert.That(snapshot.Payload.WorldTime.TickCount, Is.EqualTo(3));
            Assert.That(snapshot.Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.False);
            Assert.That(snapshot.Payload.Turnouts.TurnoutsById[ConnectionId].ActualPosition,
                Is.EqualTo(Ctc.TurnoutPosition.Reverse));
            Assert.That(moving[ConnectionId].IsMoving, Is.True);
        }

        [TestCase(1f)]
        [TestCase(3f)]
        [TestCase(10f)]
        public void UpdateAdvancesOneTickUsingUnscaledFrameTimeAndPlaybackSpeed(float speed)
        {
            float expectedDuration = Time.unscaledDeltaTime * speed;
            Assert.That(expectedDuration, Is.GreaterThan(0f));
            SetField(application, "playbackSpeed", speed);
            application.SetPaused(false);

            InvokeUpdate();

            Assert.That(application.CompletedTickCount, Is.EqualTo(1));
            Assert.That(application.ElapsedTimeSeconds, Is.EqualTo((double)expectedDuration));
            Assert.That(application.TickDurationSeconds, Is.EqualTo(expectedDuration));
            application.StepOnce();
            Assert.That(application.CompletedTickCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateDoesNotAdvanceWhilePausedOrUninitialized()
        {
            InvokeUpdate();
            Assert.That(application.CompletedTickCount, Is.Zero);

            application.SetPaused(false);
            SetField(application, "isInitialized", false);
            InvokeUpdate();
            Assert.That(application.CompletedTickCount, Is.Zero);
            Assert.That(application.ElapsedTimeSeconds, Is.Zero);
            Assert.That(application.TickDurationSeconds, Is.EqualTo(TickDurationSeconds));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDurationOrPlaybackSpeedDoesNotReplaceTheCompletedTick(float invalidValue)
        {
            application.StepOnce();
            var previousOutput = application.CompletedInterlockingOutput;

            ExecuteTick(invalidValue);
            SetField(application, "playbackSpeed", invalidValue);
            application.SetPaused(false);
            InvokeUpdate();

            Assert.That(application.CompletedTickCount, Is.EqualTo(1));
            Assert.That(application.ElapsedTimeSeconds, Is.EqualTo((double)TickDurationSeconds));
            Assert.That(application.TickDurationSeconds, Is.EqualTo(TickDurationSeconds));
            Assert.That(application.CompletedInterlockingOutput, Is.SameAs(previousOutput));
        }

        [Test]
        public void SenderOmitsOccupancyBeforeFirstTickAndWithoutCircuitSource()
        {
            Assert.That(CreateSnapshot().Payload.TrackCircuits, Is.Null);
            application.StepOnce();
            Assert.That(application.CompletedTickCount, Is.EqualTo(1));
            Assert.That(CreateSnapshot().Payload.TrackCircuits, Is.Null);
        }

        [Test]
        public void SenderOmitsTurnoutsBeforeFirstTickAndWithoutInitializedConnections()
        {
            Assert.That(CreateSnapshot().Payload.Turnouts, Is.Null);
            application.StepOnce();
            Assert.That(CreateSnapshot().Payload.Turnouts, Is.Null);

            CreateConnections(initialize: false);
            application.StepOnce();
            Assert.That(application.CompletedTurnoutsById, Is.Null);
            Assert.That(CreateSnapshot().Payload.Turnouts, Is.Null);
        }

        [Test]
        public void CompletedTickCopiesActualTurnoutPositionAndMovingState()
        {
            var connections = CreateConnections();
            Assert.That(CreateSnapshot().Payload.Turnouts, Is.Null);
            application.StepOnce();
            var normal = application.CompletedTurnoutsById;
            Assert.That(normal[ConnectionId].ActualPosition, Is.EqualTo(TrackSwitchPosition.Normal));

            Assert.That(connections.TryRequestPosition(ConnectionId, TrackSwitchPosition.Reverse,
                out string error), Is.True, error);
            var previous = CreateSnapshot();
            Assert.That(previous.Payload.WorldTime.TickCount, Is.EqualTo(1));
            Assert.That(previous.Payload.Turnouts.TurnoutsById[ConnectionId].ActualPosition,
                Is.EqualTo(Ctc.TurnoutPosition.Normal));
            Assert.That(previous.Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.False);

            application.StepOnce();
            var moving = Ctc.UnitySnapshotMessage.Parser.ParseFrom(CreateSnapshot().ToByteArray());
            Assert.That(moving.Payload.WorldTime.TickCount, Is.EqualTo(2));
            Assert.That(moving.Payload.Turnouts.TurnoutsById.Count, Is.EqualTo(1));
            Assert.That(moving.Payload.Turnouts.TurnoutsById[ConnectionId].ActualPosition,
                Is.EqualTo(Ctc.TurnoutPosition.Unknown));
            Assert.That(moving.Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.True);

            Assert.That(connections.TryConfirmPosition(ConnectionId, TrackSwitchPosition.Reverse,
                out error), Is.True, error);
            Assert.That(CreateSnapshot().Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.True);
            application.StepOnce();

            var confirmed = CreateSnapshot().Payload.Turnouts.TurnoutsById[ConnectionId];
            Assert.That(confirmed.ActualPosition, Is.EqualTo(Ctc.TurnoutPosition.Reverse));
            Assert.That(confirmed.IsMoving, Is.False);
            Assert.That(normal[ConnectionId].ActualPosition, Is.EqualTo(TrackSwitchPosition.Normal));
            Assert.That(moving.Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.True);
        }

        [Test]
        public void StepOnceAdvancesTimedTurnoutAndPublishesOnlyTheCompletedTickState()
        {
            var connections = CreateConnections();
            SetField(connections, "completeRequestsAutomatically", true);
            SetField(connections, "switchDurationSeconds", TickDurationSeconds * 2f);
            SetField(application, "playbackSpeed", 10f);
            Assert.That(connections.TryRequestPosition(ConnectionId, TrackSwitchPosition.Reverse,
                out string error), Is.True, error);

            // 一時停止中のフレーム更新では進めず、Step Onceでは倍率に関係なく1tickだけ進める。
            typeof(ApplicationSimulationController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(application, null);
            Assert.That(application.CompletedTickCount, Is.Zero);
            Assert.That(connections.TryGetState(ConnectionId, out var paused), Is.True);
            Assert.That(paused.IsMoving, Is.True);

            application.StepOnce();
            var moving = application.CompletedTurnoutsById;
            Assert.That(moving[ConnectionId].ActualPosition, Is.EqualTo(TrackSwitchPosition.Unknown));
            Assert.That(moving[ConnectionId].IsMoving, Is.True);

            application.StepOnce();
            var completed = CreateSnapshot();
            Assert.That(completed.Payload.WorldTime.TickCount, Is.EqualTo(2));
            Assert.That(completed.Payload.Turnouts.TurnoutsById[ConnectionId].ActualPosition,
                Is.EqualTo(Ctc.TurnoutPosition.Reverse));
            Assert.That(completed.Payload.Turnouts.TurnoutsById[ConnectionId].IsMoving, Is.False);
            Assert.That(moving[ConnectionId].IsMoving, Is.True);
        }

        [Test]
        public void LosingConnectionsOmitsTurnoutsOnNextCompletedTick()
        {
            var connections = CreateConnections();
            application.StepOnce();
            var previous = application.CompletedTurnoutsById;

            Assert.That(TrackConnectionLogic.TryInitialize(connections.Context, null,
                TrackSwitchPosition.Normal, out _), Is.False);
            Assert.That(application.CompletedTurnoutsById, Is.SameAs(previous));
            application.StepOnce();

            Assert.That(application.CompletedTurnoutsById, Is.Null);
            Assert.That(CreateSnapshot().Payload.Turnouts, Is.Null);
            Assert.That(previous[ConnectionId].ActualPosition, Is.EqualTo(TrackSwitchPosition.Normal));
        }

        [Test]
        public void CompletedTickSendsOccupiedAndClearCircuitsAndSurvivesSerialization()
        {
            CreateCircuitSimulation(out var source);
            source.Spans.Add(new TrackOccupiedEdgeSpan("E", 2f, 4f));
            Assert.That(CreateSnapshot().Payload.TrackCircuits, Is.Null);

            application.StepOnce();

            var message = CreateSnapshot();
            var decoded = Ctc.UnitySnapshotMessage.Parser.ParseFrom(message.ToByteArray());
            Assert.That(decoded.Payload.WorldTime.TickCount, Is.EqualTo(1));
            Assert.That(decoded.Payload.Interlocking.StationsById.ContainsKey(StationId), Is.True);
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId.Count, Is.EqualTo(2));
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId["A"], Is.True);
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId.ContainsKey("B"), Is.True);
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId["B"], Is.False);

            // 指定された場合は、Go側のgRPCテストでも同じUnity製の電文を使う。
            string outputPath = System.Environment.GetEnvironmentVariable("NAKATETSU_SNAPSHOT_TEST_OUTPUT");
            if (!string.IsNullOrEmpty(outputPath)) System.IO.File.WriteAllBytes(outputPath, message.ToByteArray());

            source.Spans.Clear();
            application.StepOnce();
            Assert.That(message.Payload.TrackCircuits.OccupiedByCircuitId["A"], Is.True);
        }

        [Test]
        public void CircuitUpdateOutsideTickDoesNotChangeTheCompletedSnapshot()
        {
            var circuits = CreateCircuitSimulation(out var source);
            source.Spans.Add(new TrackOccupiedEdgeSpan("E", 2f, 4f));
            application.StepOnce();
            var previous = application.CompletedOccupiedByCircuitId;

            source.Spans.Clear();
            source.Spans.Add(new TrackOccupiedEdgeSpan("E", 12f, 14f));
            circuits.Calculate(0f);

            Assert.That(circuits.IsOccupied("A"), Is.False);
            Assert.That(previous["A"], Is.True);
            var message = CreateSnapshot();
            Assert.That(message.Payload.WorldTime.TickCount, Is.EqualTo(1));
            Assert.That(message.Payload.TrackCircuits.OccupiedByCircuitId["A"], Is.True);
            Assert.That(message.Payload.TrackCircuits.OccupiedByCircuitId["B"], Is.False);

            application.StepOnce();
            Assert.That(application.CompletedOccupiedByCircuitId["A"], Is.False);
            Assert.That(application.CompletedOccupiedByCircuitId["B"], Is.True);
            Assert.That(previous["A"], Is.True);
        }

        [Test]
        public void AllClearIsPresentAndLosingCircuitSourceOmitsOccupancy()
        {
            CreateCircuitSimulation(out _);
            application.StepOnce();
            var decoded = Ctc.UnitySnapshotMessage.Parser.ParseFrom(CreateSnapshot().ToByteArray());
            Assert.That(decoded.Payload.TrackCircuits, Is.Not.Null);
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId.Count, Is.EqualTo(2));
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId["A"], Is.False);
            Assert.That(decoded.Payload.TrackCircuits.OccupiedByCircuitId["B"], Is.False);

            SetField(application, "trackCircuitSimulation", null);
            application.StepOnce();
            Assert.That(application.CompletedOccupiedByCircuitId, Is.Null);
            Assert.That(CreateSnapshot().Payload.TrackCircuits, Is.Null);
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
            Assert.That(application.CompletedInterlockingOutput, Is.SameAs(manager.Context.Output));
            Assert.That(station.Context.State.stateRevision, Is.EqualTo(stationRevision + 1));
            Assert.That(manager.Context.Output.StationsById.Keys, Is.EquivalentTo(new[] { StationId }));
            var snapshotRoute = manager.Context.Output.StationsById[StationId].RoutesById[RouteId];
            Assert.That(station.Context.Output.RoutesById[RouteId].IsAvailable, Is.False);
            Assert.That(snapshotRoute.IsAvailable, Is.False);
        }

        [Test]
        public void ManagementChangeOutsideTickDoesNotReplaceCompletedSnapshotUntilNextTick()
        {
            application.StepOnce();
            var completedOutput = application.CompletedInterlockingOutput;
            long completedTicks = application.CompletedTickCount;
            Assert.That(completedOutput.StationsById.ContainsKey(StationId), Is.True);

            manager.enabled = false;

            Assert.That(manager.Context.Output.StationsById, Is.Empty);
            Assert.That(application.CompletedTickCount, Is.EqualTo(completedTicks));
            Assert.That(application.CompletedInterlockingOutput, Is.SameAs(completedOutput));
            Assert.That(application.CompletedInterlockingOutput.StationsById.ContainsKey(StationId), Is.True);

            application.StepOnce();

            Assert.That(application.CompletedTickCount, Is.EqualTo(completedTicks + 1));
            Assert.That(application.CompletedInterlockingOutput, Is.SameAs(manager.Context.Output));
            Assert.That(application.CompletedInterlockingOutput.StationsById, Is.Empty);
            Assert.That(completedOutput.StationsById.ContainsKey(StationId), Is.True);
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
            Assert.That(application.CompletedInterlockingOutput, Is.SameAs(manager.Context.Output));
            Assert.That(manager.Context.Output, Is.Not.SameAs(previousSnapshot));
            Assert.That(previousSnapshot.StationsById.ContainsKey(StationId), Is.True);
        }

        private Ctc.UnitySnapshotMessage CreateSnapshot()
        {
            // 非アクティブのまま電文生成だけを呼び、ネットワーク接続は開始しない。
            var sender = CreateInactiveGameObject("Sender").AddComponent<UnitySnapshotSender>();
            SetField(sender, "simulation", application);
            return (Ctc.UnitySnapshotMessage)typeof(UnitySnapshotSender)
                .GetMethod("CreateSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sender, null);
        }

        private void ExecuteTick(float durationSeconds)
        {
            typeof(ApplicationSimulationController).GetMethod("ExecuteTick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(application, new object[] { durationSeconds });
        }

        private void InvokeUpdate()
        {
            typeof(ApplicationSimulationController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(application, null);
        }

        private TrackCircuitSimulationController CreateCircuitSimulation(out OccupancySource source)
        {
            var graph = CreateInactiveGameObject("Graph").AddComponent<TrackGraphController>();
            var definition = new TrackGraphDefinition();
            definition.edges.Add(new TrackEdgeDefinition
            {
                edgeId = "E", startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 20f,
                distanceMap = new List<TrackEdgeDistanceSample>
                {
                    new() { distanceOnGeometryM = 0f, distanceOnEdgeM = 0f },
                    new() { distanceOnGeometryM = 20f, distanceOnEdgeM = 20f }
                }
            });
            definition.circuits.Add(new TrackCircuitDefinition
            {
                circuitId = "A", sections = new List<TrackCircuitSection>
                {
                    new() { edgeId = "E", startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 10f }
                }
            });
            definition.circuits.Add(new TrackCircuitDefinition
            {
                circuitId = "B", sections = new List<TrackCircuitSection>
                {
                    new() { edgeId = "E", startDistanceOnGeometryM = 10f, endDistanceOnGeometryM = 20f }
                }
            });
            Assert.That(graph.Context.TryBuildLookups(definition, out string error), Is.True, error);
            var circuits = CreateInactiveGameObject("Circuits").AddComponent<TrackCircuitSimulationController>();
            circuits.SetTrackGraphController(graph);
            source = new OccupancySource();
            Assert.That(circuits.RegisterSource(source), Is.True);
            SetField(application, "trackCircuitSimulation", circuits);
            return circuits;
        }

        private TrackConnectionController CreateConnections(bool initialize = true)
        {
            var graph = CreateInactiveGameObject("Connections").AddComponent<TrackGraphController>();
            var definition = new TrackGraphDefinition();
            definition.nodes.Add(new TrackNodeDefinition
            {
                nodeId = "switch", connectedEdgeIds = new() { "common", "normal", "reverse" }
            });
            foreach (string id in new[] { "common", "normal", "reverse" })
            {
                definition.nodes.Add(new TrackNodeDefinition
                {
                    nodeId = id + "-end", connectedEdgeIds = new() { id }
                });
                definition.edges.Add(new TrackEdgeDefinition
                {
                    edgeId = id, nodeAId = "switch", nodeBId = id + "-end"
                });
            }
            definition.connections.Add(new TrackConnectionDefinition
            {
                connectionId = ConnectionId, nodeId = "switch",
                edgePairs = new()
                {
                    new() { pairId = "normal", edgeAId = "common", edgeBId = "normal", condition = TrackConnectionCondition.Normal },
                    new() { pairId = "reverse", edgeAId = "common", edgeBId = "reverse", condition = TrackConnectionCondition.Reverse }
                }
            });
            Assert.That(graph.Context.TryBuildLookups(definition, out string error), Is.True, error);
            var connections = graph.gameObject.AddComponent<TrackConnectionController>();
            SetField(connections, "completeRequestsAutomatically", false);
            if (initialize)
                Assert.That(connections.TryInitialize(out error), Is.True, error);
            SetField(application, "trackConnections", connections);
            return connections;
        }

        private sealed class OccupancySource : ITrackOccupancySource
        {
            public readonly List<TrackOccupiedEdgeSpan> Spans = new();

            public bool TryGetOccupiedEdges(List<TrackOccupiedEdgeSpan> destination)
            {
                destination.AddRange(Spans);
                return true;
            }
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
