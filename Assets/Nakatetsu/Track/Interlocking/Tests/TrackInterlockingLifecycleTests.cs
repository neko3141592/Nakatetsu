using System;
using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingLifecycleTests
    {
        private GameObject gameObject;
        private TrackInterlockingController interlocking;
        private TrackConnectionController connectionController;
        private TrackAtcController atc;
        private TrackCircuitSimulationState circuits;
        private TrackConnectionContext connections;
        private TrackInterlockingDefinition definition;
        private InterlockingRoute route;
        private TrackInterlockingRouteState state;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Interlocking lifecycle test");
            gameObject.SetActive(false);
            var circuitController = gameObject.AddComponent<TrackCircuitSimulationController>();
            connectionController = gameObject.AddComponent<TrackConnectionController>();
            interlocking = gameObject.AddComponent<TrackInterlockingController>();
            atc = gameObject.AddComponent<TrackAtcController>();
            SetField(interlocking, "trackCircuitSimulation", circuitController);
            SetField(interlocking, "trackConnectionController", connectionController);
            SetField(atc, "trackCircuitSimulation", circuitController);
            SetField(atc, "trackConnectionController", connectionController);
            SetField(atc, "interlockings", new List<TrackInterlockingController> { interlocking });
            circuits = circuitController.Context.State;
            connections = connectionController.Context;
            typeof(TrackConnectionContext).GetProperty(nameof(TrackConnectionContext.IsInitialized)).SetValue(connections, true);

            foreach (string circuitId in new[] { "Entry", "Arrival", "Overlap", "Extra", "Approach", "Other",
                "MainLock", "CommonLock", "AdditionalLock" })
            {
                SetOccupied(circuitId, false);
            }
            foreach (string connectionId in new[] { "Main", "Common", "Additional" })
            {
                SetSwitch(connectionId, TrackSwitchPosition.Normal);
            }

            route = new InterlockingRoute
            {
                routeId = "ArrivalRoute",
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Arrival",
                routeLockTrackCircuitIds = new List<string> { "Entry", "Arrival" },
                requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Main") },
                approachLock = new ApproachLockDefinition
                {
                    trackCircuitIds = new List<string> { "Approach" }, releaseSeconds = 5f
                },
                overrunProtection = new OverrunProtectionDefinition
                {
                    common = new OverrunProtectionResources
                    {
                        clearTrackCircuitIds = new List<string> { "Overlap" },
                        requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Common") }
                    },
                    normalAdditional = new OverrunProtectionResources
                    {
                        clearTrackCircuitIds = new List<string> { "Extra" },
                        requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Additional") }
                    },
                    release = new OverrunReleaseDefinition
                    {
                        mode = OverrunReleaseMode.TimedAfterArrival,
                        triggerTrackCircuitId = "Arrival",
                        releaseSeconds = 10f
                    }
                }
            };
            definition = new TrackInterlockingDefinition();
            definition.routes.Add(route);
            definition.routes.Add(new InterlockingRoute
            {
                routeId = "ReuseMain", routeLockTrackCircuitIds = new List<string> { "Entry" },
                requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Main", TrackSwitchPosition.Reverse) },
                conflictRouteIds = new List<string> { route.routeId }
            });
            definition.routes.Add(new InterlockingRoute
            {
                routeId = "ReuseOverrun", routeLockTrackCircuitIds = new List<string> { "Overlap" },
                requiredTurnouts = new List<TurnoutRequirement> { RequireTurnout("Common", TrackSwitchPosition.Reverse) }
            });
            foreach (string connectionId in new[] { "Main", "Common", "Additional" })
            {
                definition.turnoutTrackCircuitLocks.Add(new TurnoutTrackCircuitLock
                {
                    connectionId = connectionId,
                    trackCircuitIds = new List<string> { connectionId + "Lock" }
                });
            }
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ReservationDoesNotMeanEstablishedOrStartTimer()
        {
            Request();
            Assert.That(state.PathEstablished, Is.False);
            Assert.That(state.ProceedAllowed, Is.False);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Setting));
            Tick(100f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Established));
            Assert.That(state.OverrunArrivalDetected, Is.False);
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.Zero);
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.True);
        }

        [TestCase("Main")]
        [TestCase("Common")]
        [TestCase("Additional")]
        public void ControllerSetsAllRequiredTurnoutsBeforeAllowingEntry(string connectionId)
        {
            SetSwitch(connectionId, TrackSwitchPosition.Reverse);
            Request();
            Tick();
            Assert.That(state.PathEstablished, Is.False);
            Assert.That(state.ProceedAllowed, Is.False);
            interlocking.ApplyOutput(0f);
            Assert.That(connections.TryGetState(connectionId, out var turnout), Is.True);
            Assert.That(turnout.ActualPosition, Is.EqualTo(TrackSwitchPosition.Normal));
            Assert.That(state.ProceedAllowed, Is.False, "転換完了後のtickで実位置を照査すること");
            Tick();
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.True);
        }

        [TestCase("Common", true)]
        [TestCase("Additional", true)]
        [TestCase("Main", false)]
        [TestCase("Common", false)]
        [TestCase("Additional", false)]
        public void MovingOrUnknownTurnoutDoesNotEstablishPath(string connectionId, bool moving)
        {
            Request();
            SetSwitch(connectionId, moving ? TrackSwitchPosition.Normal : TrackSwitchPosition.Unknown, moving);
            Tick();
            Assert.That(state.PathEstablished, Is.False);
            Assert.That(state.ProceedAllowed, Is.False);
        }

        [TestCase("Common", false)]
        [TestCase("Common", true)]
        [TestCase("Additional", false)]
        [TestCase("Additional", true)]
        public void OverrunTurnoutObeysTrackCircuitLock(string connectionId, bool unavailable)
        {
            SetSwitch(connectionId, TrackSwitchPosition.Reverse);
            if (unavailable) RemoveCircuit(connectionId + "Lock");
            else SetOccupied(connectionId + "Lock", true);
            Request();
            Tick();
            Assert.That(interlocking.CanRequestTurnoutPosition(route.routeId, connectionId), Is.False);
            interlocking.ApplyOutput(0f);
            connections.TryGetState(connectionId, out var turnout);
            Assert.That(turnout.ActualPosition, Is.EqualTo(TrackSwitchPosition.Reverse));
            Assert.That(state.ProceedAllowed, Is.False);
        }

        [Test]
        public void EntryKeepsPathButRemovesProceedPermissionAndTurnoutRequests()
        {
            Request();
            Tick();
            SetOccupied("Entry", true);
            Tick();
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.False);
            Assert.That(interlocking.CanRequestTurnoutPosition(route.routeId, "Common"), Is.False);
            atc.Calculate(0f);
            var input = atc.Context.Input.RoutesById[route.routeId];
            Assert.That(input.PathEstablished, Is.True);
            Assert.That(input.ProceedAllowed, Is.False);
            Assert.That(input.RouteLocked, Is.True);
            Assert.That(input.OverrunMode, Is.EqualTo(OverrunProtectionMode.Normal));
            SetSwitch("Common", TrackSwitchPosition.Reverse);
            interlocking.ApplyOutput(0f);
            Tick();
            Assert.That(state.PathEstablished, Is.False);
            connections.TryGetState("Common", out var turnout);
            Assert.That(turnout.ActualPosition, Is.EqualTo(TrackSwitchPosition.Reverse));
        }

        [Test]
        public void ArrivalStartsFullTimerOnlyOnce()
        {
            Request();
            Tick(100f);
            SetOccupied("Entry", true);
            Tick(100f);
            Assert.That(state.OverrunArrivalDetected, Is.False);
            SetOccupied("Arrival", true);
            Tick(3f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.EqualTo(10f));
            Tick(3f);
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.EqualTo(7f));
            SetOccupied("Arrival", false);
            Tick(1f);
            SetOccupied("Arrival", true);
            Tick(1f);
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void OccupiedArrivalAtRequestIsRejected()
        {
            SetOccupied("Arrival", true);
            Initialize();
            Assert.That(interlocking.TryRequestRoute(route.routeId, out _), Is.False);
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [Test]
        public void ArrivalBeforeProtectionIsEstablishedDoesNotStartTimer()
        {
            SetSwitch("Common", TrackSwitchPosition.Reverse);
            Request();
            SetOccupied("Entry", true);
            SetOccupied("Arrival", true);
            Tick(100f);
            SetSwitch("Common", TrackSwitchPosition.Normal);
            Tick(100f);
            Assert.That(state.OverrunArrivalDetected, Is.False);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Setting));
            Assert.That(state.PathEstablished, Is.False);
        }

        [Test]
        public void MainReleaseKeepsOverrunAndDoesNotRelockForAnotherTrain()
        {
            Arrive();
            SetOccupied("Entry", false);
            SetOccupied("Arrival", false);
            Tick(1f);
            Assert.That(state.RouteLocked, Is.False);
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.True);
            Assert.That(interlocking.TryRequestRoute("ReuseOverrun", out _), Is.False);
            Assert.That(interlocking.TryRequestRoute("ReuseMain", out string error), Is.True, error);
            SetOccupied("Entry", true);
            Tick(1f);
            Assert.That(state.RouteLocked, Is.False, "別列車の在線を旧進路の通過状態へ反映しないこと");
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Tick(8f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [Test]
        public void TimedReleaseFreesOnlyOverrunWhileMainIsOccupied()
        {
            Arrive();
            Tick(10f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(state.RouteLocked, Is.True);
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.False);
            Assert.That(interlocking.TryRequestRoute("ReuseOverrun", out string error), Is.True, error);
            interlocking.ApplyOutput(0f);
            Tick();
            Assert.That(state.PathEstablished, Is.True, "解放済みの過走防護用転轍機には依存しないこと");
            Assert.That(interlocking.CanRequestTurnoutPosition(route.routeId, "Common"), Is.False);
        }

        [Test]
        public void WithRouteReleaseWaitsForMainPassage()
        {
            route.overrunProtection.release.mode = OverrunReleaseMode.WithRouteRelease;
            Arrive();
            Tick(100f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Established));
            SetOccupied("Entry", false);
            SetOccupied("Arrival", false);
            Tick();
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationBeforeEntryWaitsForApproachLock(bool established)
        {
            Request();
            if (established) Tick();
            SetOccupied("Approach", true);
            Assert.That(interlocking.TryCancelRoute(route.routeId, out string error), Is.True, error);
            Tick(4f);
            Assert.That(state.RouteLocked, Is.True);
            Assert.That(state.ApproachLocked, Is.True);
            Assert.That(state.ProceedAllowed, Is.False);
            Assert.That(interlocking.TryRequestRoute("ReuseOverrun", out _), Is.False);
            Tick(1f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [TestCase("Main")]
        [TestCase("Common")]
        [TestCase("Additional")]
        public void CancellationDuringSettingWaitsForMotionButDoesNotDemandRequiredPosition(string connectionId)
        {
            SetSwitch(connectionId, TrackSwitchPosition.Unknown, true);
            Request();
            Assert.That(interlocking.TryCancelRoute(route.routeId, out _), Is.True);
            Tick(100f);
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.True);
            Assert.That(interlocking.CanRequestTurnoutPosition(route.routeId, connectionId), Is.False);
            SetSwitch(connectionId, TrackSwitchPosition.Reverse);
            Tick();
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [Test]
        public void CancellationAfterEntryRetainsMainAndOverrunUntilTheirReleaseConditions()
        {
            Request();
            Tick();
            SetOccupied("Entry", true);
            Tick();
            Assert.That(interlocking.TryCancelRoute(route.routeId, out _), Is.True);
            Tick(100f);
            Assert.That(state.RouteLocked, Is.True);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Established));
            atc.Calculate(0f);
            Assert.That(atc.Context.Input.RoutesById[route.routeId].CancelPending, Is.True);
            Assert.That(atc.Context.Input.RoutesById[route.routeId].ProceedAllowed, Is.False);
            SetOccupied("Arrival", true);
            Tick();
            Tick(10f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
            Assert.That(state.RouteLocked, Is.True);
            SetOccupied("Entry", false);
            SetOccupied("Arrival", false);
            Tick();
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [TestCase("Overlap", false)]
        [TestCase("Overlap", true)]
        [TestCase("Extra", false)]
        [TestCase("Extra", true)]
        public void OverrunDuringTimerBlocksAutomaticReleaseEvenAfterCircuitClears(string circuitId, bool unavailable)
        {
            Arrive();
            if (unavailable) RemoveCircuit(circuitId);
            else SetOccupied(circuitId, true);
            Tick(10f);
            Assert.That(state.OverrunAutomaticReleaseBlocked, Is.True);
            Assert.That(state.PathEstablished, Is.False);
            SetOccupied(circuitId, false);
            SetOccupied("Entry", false);
            SetOccupied("Arrival", false);
            interlocking.TryCancelRoute(route.routeId, out _);
            Tick(100f);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.True);
            Assert.That(interlocking.TryRequestRoute("ReuseOverrun", out _), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TimerExpiryWaitsForConfirmedOverrunTurnout(bool moving)
        {
            Arrive();
            SetSwitch("Additional", moving ? TrackSwitchPosition.Normal : TrackSwitchPosition.Reverse, moving);
            Tick(10f);
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.Zero);
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(state.PathEstablished, Is.False);
            SetSwitch("Additional", TrackSwitchPosition.Normal);
            Tick();
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.Released));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidElapsedTimeDoesNotAdvanceReleaseTimer(float deltaTime)
        {
            Arrive();
            Tick(deltaTime);
            Assert.That(state.OverrunProtectionReleaseRemainingSeconds, Is.EqualTo(10f));
            Assert.That(state.overrunProtectionPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
        }

        [TestCase("Common")]
        [TestCase("Additional")]
        public void ProtectionTurnoutWithoutTrackCircuitLockIsRejected(string connectionId)
        {
            definition.turnoutTrackCircuitLocks.RemoveAll(turnoutLock => turnoutLock.connectionId == connectionId);
            Assert.That(TrackInterlockingLogic.TryInitialize(interlocking.Context, definition, out string error), Is.False);
            Assert.That(error, Does.Contain("track circuit lock"));
        }

        [TestCase("trigger")]
        [TestCase("negative")]
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("mode")]
        public void InvalidReleaseDefinitionIsRejected(string invalidPart)
        {
            var release = route.overrunProtection.release;
            switch (invalidPart)
            {
                case "trigger": release.triggerTrackCircuitId = "Other"; break;
                case "negative": release.releaseSeconds = -1f; break;
                case "nan": release.releaseSeconds = float.NaN; break;
                case "infinity": release.releaseSeconds = float.PositiveInfinity; break;
                case "mode": release.mode = (OverrunReleaseMode)99; break;
            }
            Assert.That(TrackInterlockingLogic.TryInitialize(interlocking.Context, definition, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void RouteWithoutProtectionKeepsExistingPassageAndReleaseBehavior()
        {
            route.overrunProtection = null;
            Request();
            Tick();
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.True);
            SetOccupied("Entry", true);
            SetOccupied("Arrival", true);
            Tick();
            Assert.That(state.PathEstablished, Is.True);
            Assert.That(state.ProceedAllowed, Is.False);
            SetOccupied("Entry", false);
            SetOccupied("Arrival", false);
            Tick();
            Assert.That(interlocking.TryGetRouteState(route.routeId, out _), Is.False);
        }

        [Test]
        public void AtcEntryRequiresEstablishedPathAndProceedPermission()
        {
            Request();
            var context = atc.Context;
            var before = new TrackAtcGraphEdge
            {
                atcEdgeId = "Before", atcNodeAId = "A", atcNodeBId = "B", trackCircuitId = "Approach",
                controlKind = TrackAtcEdgeControlKind.Block, direction = TrackAtcTravelDirection.AtoB
            };
            var first = new TrackAtcGraphEdge
            {
                atcEdgeId = "First", atcNodeAId = "B", atcNodeBId = "C", trackCircuitId = "Entry",
                controlKind = TrackAtcEdgeControlKind.Interlocking
            };
            var second = new TrackAtcGraphEdge { atcEdgeId = "Second", atcNodeAId = "C", atcNodeBId = "D" };
            context.Workspace.atcEdgesById.Add(first.atcEdgeId, first);
            context.Workspace.atcEdgesById.Add(second.atcEdgeId, second);
            context.Workspace.atcNodesById.Add("B", new TrackAtcGraphNode
            {
                atcNodeId = "B", connectedAtcEdgeIds = new List<string> { "Before", "First" }
            });
            context.Workspace.atcRoutesById.Add("AtcRoute", new TrackAtcRouteDefinition
            {
                atcRouteId = "AtcRoute", interlockingRouteId = route.routeId,
                atcEdgeIds = new List<string> { "First", "Second" }
            });
            atc.Calculate(0f);
            // 入力が不整合でも、予約だけを開通とみなさない。
            context.Input.RoutesById[route.routeId].ProceedAllowed = true;
            Assert.That(TrackAtcLogic.TryResolveNextEdgeOnBlock(context, before, out var next), Is.True);
            Assert.That(next, Is.Null);
            Tick();
            atc.Calculate(0f);
            Assert.That(TrackAtcLogic.TryResolveNextEdgeOnBlock(context, before, out next), Is.True);
            Assert.That(next, Is.SameAs(first));
            SetOccupied("Entry", true);
            Tick();
            atc.Calculate(0f);
            Assert.That(context.Input.RoutesById[route.routeId].PathEstablished, Is.True);
            Assert.That(TrackAtcLogic.TryResolveNextEdgeOnBlock(context, before, out next), Is.True);
            Assert.That(next, Is.Null);
        }

        private void Initialize()
        {
            Assert.That(TrackInterlockingLogic.TryInitialize(interlocking.Context, definition, out string error), Is.True, error);
        }

        private void Request()
        {
            Initialize();
            Assert.That(interlocking.TryRequestRoute(route.routeId, out string error), Is.True, error);
            Assert.That(interlocking.TryGetRouteState(route.routeId, out state), Is.True);
        }

        private void Arrive()
        {
            Request();
            Tick();
            SetOccupied("Entry", true);
            Tick();
            SetOccupied("Arrival", true);
            Tick();
        }

        private void Tick(float deltaTimeSeconds = 0f) => interlocking.Calculate(deltaTimeSeconds);

        private static TurnoutRequirement RequireTurnout(string connectionId, TrackSwitchPosition position = TrackSwitchPosition.Normal) =>
            new() { connectionId = connectionId, requiredPosition = position };

        private void SetOccupied(string circuitId, bool occupied) =>
            typeof(TrackCircuitSimulationState).GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(circuits, new object[] { circuitId, occupied });

        private void RemoveCircuit(string circuitId) =>
            ((Dictionary<string, bool>)typeof(TrackCircuitSimulationState)
                .GetField("occupiedByCircuitId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(circuits)).Remove(circuitId);

        private void SetSwitch(string connectionId, TrackSwitchPosition position, bool moving = false)
        {
            var states = (Dictionary<string, TrackConnectionState>)typeof(TrackConnectionContext)
                .GetField("StatesById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connections);
            states[connectionId] = (TrackConnectionState)Activator.CreateInstance(typeof(TrackConnectionState),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { TrackSwitchPosition.Normal, position, moving }, null);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
