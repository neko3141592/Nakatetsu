using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcPositionTests
    {
        [TestCase(10f, true, 20f, 55f, 25f)]
        [TestCase(-10f, true, 20f, 45f, 15f)]
        [TestCase(10f, false, 80f, 45f, 75f)]
        [TestCase(-10f, false, 80f, 55f, 85f)]
        public void IntegratesSignedMeasuredSpeedForBothEnds(
            float speed, bool frontFacesAtoB, float rearDistance, float expectedFront, float expectedRear)
        {
            var context = CreateContext(CreateLine(), 50f, rearDistance, frontFacesAtoB);
            context.Input.deltaTimeSeconds = 0.5f;
            context.Input.signedSpeedMps = speed;
            // レバーサ位置や運転台の前後は実測の移動方向を変えない。
            context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedFront));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedRear));
            Assert.That(context.State.operation.currentPosition.atcEdgeId, Is.EqualTo(context.State.position.frontPosition.atcEdgeId));
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedFront));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TracksBothEndsWithKeyOffOrCabUnavailable(bool hasCabState)
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.hasCabState = hasCabState;
            context.Input.cab.isKeyInserted = false;
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(60f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.operation.isAtcEnabled, Is.False);

            context.Input.hasCabState = true;
            context.Input.cab.carIndex = 1;
            context.Input.cab.isFrontCab = false;
            context.Input.cab.isKeyInserted = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.operation.isAtcEnabled, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UniqueConnectionIsTraversedWithoutValidTelegram(bool invalidTelegram)
        {
            var context = CreateContext(CreateLine(), 95f, 20f);
            context.Input.signedSpeedMps = 10f;
            if (invalidTelegram)
            {
                context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = false };
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void EdgeOrientationChangesAtNodeBEntry()
        {
            var graph = CreateLine();
            graph.atcEdge[1].atcNodeAId = "n2";
            graph.atcEdge[1].atcNodeBId = "n1";
            var context = CreateContext(graph, 95f, 20f);
            context.Input.signedSpeedMps = 10f;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.False);
            Assert.That(context.State.operation.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(85f));
            Assert.That(context.State.operation.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
        }

        [Test]
        public void ReverserDirectionIsKeptWhileMeasuredSpeedMovesOppositeDirection()
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.signedSpeedMps = -10f;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(10f));
            Assert.That(context.State.operation.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.AtoB));
        }

        [Test]
        public void BackwardTravelUsesMeasuredDirectionAndCarriesDistanceAcrossEdges()
        {
            var context = CreateContext(CreateLine(), 5f, 0f, true, "b");
            context.Input.signedSpeedMps = -10f;
            context.Input.frontTelegram = Telegram("b", TrackAtcTravelDirection.BtoA, "b", "a");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(90f));
        }

        [Test]
        public void ExactBoundarySelectsNextEdgeAndTerminalEndpointRemainsKnown()
        {
            var context = CreateContext(CreateLine(), 95f, 20f);
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.Zero);

            context = CreateContext(CreateLine(), 95f, 20f, true, "b");
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(100f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            context.Input.signedSpeedMps = 1f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
        }

        [Test]
        public void TelegramChoosesBranchBeforeStaticRouteDefinitions()
        {
            var graph = CreateFork();
            graph.routes.Add(Route("a", "b"));
            var context = CreateContext(graph, 95f, 20f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "c");
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void AcceptedPatternResolvesSelectedReceiverBranchDuringSignalGrace()
        {
            var graph = CreateFork();
            graph.maximumOperatingSpeedKmh = 72f;
            var context = CreateContext(graph, 95f, 20f);
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "c");
            context.Input.frontTelegram.routeAtoB
                .overrunProtectionMode = OverrunProtectionMode.Normal;
            context.Input.deltaTimeSeconds = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.pattern.isValid, Is.True);

            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.5f;
            context.Input.signedSpeedMps = 12f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(1f));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(26f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.validation.noSignalElapsedSeconds, Is.EqualTo(0.5f));
            Assert.That(context.State.pattern.distanceOnPathM, Is.EqualTo(101f));
            Assert.That(context.State.pattern.isValid, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.False);
        }

        [Test]
        public void InactiveRelativePositionDoesNotFollowConflictingUnusedTelegram()
        {
            var context = CreateContext(CreateFork(), 95f, 94f);
            context.Input.hasCabState = false;
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            context.Input.rearTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "c");
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(4f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.operation.hasCurrentPosition, Is.False);
        }

        [Test]
        public void CarriesTelegramPathAcrossSeveralEdgesInOneUpdate()
        {
            var graph = CreateFork();
            graph.atcEdge.Add(Edge("d", "n2", "n4"));
            graph.atcEdge.Add(Edge("e", "n2", "n5"));
            var context = CreateContext(graph, 95f, 94f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b", "d");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            context.Input.signedSpeedMps = 120f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("d"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(15f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(14f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void SoleDefinedRouteResolvesForkWithoutAnyOpenRouteState()
        {
            var graph = CreateFork();
            graph.atcEdge[1].direction = TrackAtcTravelDirection.BtoA;
            graph.atcEdge[1].controlKind = TrackAtcEdgeControlKind.Interlocking;
            graph.routes.Add(Route("a", "b"));
            graph.routes.Add(Route("a", "b"));
            var context = CreateContext(graph, 95f, 20f);
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosedRouteDefinitionAlsoResolvesBackwardTravel(bool frontFacesAtoB)
        {
            var graph = CreateFork();
            graph.routes.Add(Route("a", "b"));
            var context = CreateContext(graph, 5f, frontFacesAtoB ? 0f : 50f, frontFacesAtoB, "b");
            context.Input.signedSpeedMps = 10f;
            if (frontFacesAtoB)
            {
                context.Input.signedSpeedMps = -10f;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.EqualTo(frontFacesAtoB));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void TelegramForOppositeDirectionDoesNotResolveAnAmbiguousFork()
        {
            var context = CreateContext(CreateFork(), 95f, 20f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.BtoA, "a", "b");
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("a"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AmbiguousSelectedBranchKeepsLastKnownPositionsAndLatchesFailure(bool multipleRoutes)
        {
            var graph = CreateFork();
            if (multipleRoutes)
            {
                graph.routes.Add(Route("a", "b"));
                graph.routes.Add(Route("a", "c"));
            }
            var context = CreateContext(graph, 95f, 20f);
            var front = context.State.position.frontPosition;
            var rear = context.State.position.rearPosition;
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition, Is.EqualTo(front));
            Assert.That(context.State.position.rearPosition, Is.EqualTo(rear));
            Assert.That(context.State.position.isFrontPositionKnown, Is.False);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.operation.hasCurrentPosition, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
            // 速度照査の選択と、位置が使用可能かは別に保持する。
            Assert.That(context.State.operation.isAtcEnabled, Is.True);

            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.False);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
            Assert.That(context.State.position.frontPosition, Is.EqualTo(front));
        }

        [Test]
        public void UnresolvedUnusedPositionDoesNotFaultSelectedReceiver()
        {
            var context = CreateMergingContext();
            context.Input.signedSpeedMps = 10f;
            var lastRear = context.State.position.rearPosition;
            TrainAtcLogic.Calculate(context);

            Assert.That(context.State.position.isFrontPositionKnown, Is.True);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.position.rearPosition, Is.SameAs(lastRear));
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.Output.pattern.isSpeedIndicated, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.False);

            // 非使用側の電文が復旧しても、相対位置が一意でない間は正常とは扱わない。
            context.Input.rearTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.False);
        }

        [TestCase(20f, 0.02f, false)]
        [TestCase(73f, 0.015f, true)]
        public void RearReceiverCrossingOccupiedTurnoutDoesNotFaultFrontReceiver(
            float speedKmh, float deltaTimeSeconds, bool expectedOverspeedBrake)
        {
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 105f };
            var approach = Edge("20T", "start", "turnout-entry");
            approach.lengthM = 1000f;
            var loopTurnout = Edge("21T:0", "turnout-entry", "loop-entry");
            loopTurnout.trackCircuitId = "21T";
            loopTurnout.lengthM = 120.66534f;
            var mainTurnout = Edge("21T:1", "turnout-entry", "main-entry");
            mainTurnout.trackCircuitId = "21T";
            mainTurnout.lengthM = 120f;
            var loop = Edge("1RT", "loop-entry", "loop-end");
            loop.lengthM = 280f;
            loop.speedLimitSections.Add(new TrackAtcSpeedLimitSection
            {
                startDistanceOnAtcEdgeM = 0f,
                endDistanceOnAtcEdgeM = loop.lengthM,
                speedLimitKmh = 45f
            });
            graph.atcEdge.AddRange(new[] { approach, loopTurnout, mainTurnout, loop });
            graph.routes.Add(Route("21T:0", "1RT"));

            float frontDistanceM = 180f - loopTurnout.lengthM - 0.1f;
            var context = CreateContext(graph, Position("1RT", frontDistanceM), Position("20T", 999.9f), 180f);
            context.Input.frontTelegram = Telegram("1RT", TrackAtcTravelDirection.AtoB, "1RT");
            context.Input.frontTelegram.routeAtoB.overrunProtectionMode = OverrunProtectionMode.Restricted;
            // 先頭が21Tを占有済みなので、後側の20T電文は正常な停止経路だけを持つ。
            context.Input.rearTelegram = Telegram("20T", TrackAtcTravelDirection.AtoB, "20T");
            context.Input.deltaTimeSeconds = deltaTimeSeconds;
            context.Input.signedSpeedMps = speedKmh / 3.6f;
            TrainAtcLogic.Calculate(context);

            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("21T:0"));
            Assert.That(context.State.operation.currentPosition.atcEdgeId, Is.EqualTo("1RT"));
            Assert.That(context.State.protectionMode.overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.Restricted));
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.Output.pattern.isSpeedIndicated, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.EqualTo(expectedOverspeedBrake));
        }

        [Test]
        public void UnusedPositionRecoversOnSameEdgeAndCanBeSelectedWithoutCorrection()
        {
            var context = CreateMergingContext();
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);

            context.Input.deltaTimeSeconds = 4f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isRearPositionKnown, Is.True);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(70f));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(10f));

            context.Input.cab.isFrontCab = false;
            context.Input.cab.carIndex = 1;
            context.Input.deltaTimeSeconds = 0f;
            context.Input.signedSpeedMps = 0f;
            context.Input.rearTelegram = Telegram("b", TrackAtcTravelDirection.BtoA, "b");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.operation.selectedReceiver, Is.EqualTo(TrainAtcReceiverSide.Rear));
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(10f));
            Assert.That(context.State.operation.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
            Assert.That(context.State.pattern.atcEdgePath, Is.EqualTo(new[] { "b" }));
            Assert.That(context.State.pattern.pathStartTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
            Assert.That(context.State.validation.result, Is.EqualTo(TrainAtcValidationResult.Adopt));
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectingUnresolvedSideFaultsWithoutRetainingOtherReceiversPattern(bool hasNewTelegram)
        {
            var context = CreateMergingContext();
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isAtcHealthy, Is.True);

            context.Input.cab.isFrontCab = false;
            context.Input.cab.carIndex = 1;
            context.Input.deltaTimeSeconds = 0f;
            context.Input.signedSpeedMps = 0f;
            context.Input.rearTelegram = hasNewTelegram
                ? Telegram("a", TrackAtcTravelDirection.BtoA, "a")
                : null;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.True);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.operation.selectedReceiver, Is.EqualTo(TrainAtcReceiverSide.Rear));
            Assert.That(context.State.operation.hasCurrentPosition, Is.False);
            Assert.That(context.State.pattern.isValid, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
            Assert.That(context.Output.pattern.isSpeedIndicated, Is.False);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.True);
        }

        [Test]
        public void SelectedUnknownPositionCanRecoverFromFreshOppositeAnchor()
        {
            var context = CreateMergingContext();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);

            context.Input.cab.isFrontCab = false;
            context.Input.cab.carIndex = 1;
            context.Input.signedSpeedMps = 5f;
            context.Input.deltaTimeSeconds = 10f;
            context.Input.rearTelegram = Telegram("b", TrackAtcTravelDirection.BtoA, "b");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.True);
            Assert.That(context.State.position.isRearPositionKnown, Is.True);
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(10f));
            Assert.That(context.State.isAtcHealthy, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RelativePositionTraversesSeveralUniqueEdgesWithOrientationChanges(bool usesRearReceiver)
        {
            var graph = CreateLine();
            graph.atcEdge[1].atcNodeAId = "n2";
            graph.atcEdge[1].atcNodeBId = "n1";
            graph.atcEdge.Add(Edge("c", "n2", "n3"));
            var context = CreateContext(graph, Position("c", 50f), Position("a", 50f), 200f);
            context.Input.deltaTimeSeconds = 2f;
            context.Input.signedSpeedMps = usesRearReceiver ? -5f : 5f;
            if (usesRearReceiver)
            {
                context.Input.cab.isFrontCab = false;
                context.Input.cab.carIndex = 1;
                context.Input.rearTelegram = Telegram("a", TrackAtcTravelDirection.BtoA, "a");
            }
            else
            {
                context.Input.frontTelegram = Telegram("c", TrackAtcTravelDirection.AtoB, "c");
            }
            TrainAtcLogic.Calculate(context);
            float expectedDistance = usesRearReceiver ? 40f : 60f;
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedDistance));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedDistance));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.position.rearPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [TestCase(false, ReverserPosition.Forward, TrackAtcTravelDirection.AtoB)]
        [TestCase(false, ReverserPosition.Reverse, TrackAtcTravelDirection.BtoA)]
        [TestCase(true, ReverserPosition.Forward, TrackAtcTravelDirection.BtoA)]
        [TestCase(true, ReverserPosition.Reverse, TrackAtcTravelDirection.AtoB)]
        public void ReceiverSelectionFollowsCabAndDirectionFollowsReverser(
            bool usesRearReceiver, ReverserPosition reverser, TrackAtcTravelDirection expectedDirection)
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.cab.isFrontCab = !usesRearReceiver;
            context.Input.cab.carIndex = usesRearReceiver ? 1 : 0;
            context.Input.cab.reverserPosition = reverser;
            context.Input.signedSpeedMps = -5f;
            var telegram = Telegram("a", expectedDirection, "a");
            if (usesRearReceiver)
            {
                context.Input.rearTelegram = telegram;
            }
            else
            {
                context.Input.frontTelegram = telegram;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.operation.selectedReceiver,
                Is.EqualTo(usesRearReceiver ? TrainAtcReceiverSide.Rear : TrainAtcReceiverSide.Front));
            Assert.That(context.State.operation.currentTravelDirection, Is.EqualTo(expectedDirection));
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM,
                Is.EqualTo(usesRearReceiver ? 15f : 45f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [TestCase("rearNaN")]
        [TestCase("rearInfinity")]
        [TestCase("rearNegative")]
        [TestCase("reversedOffsets")]
        public void InvalidUnusedReceiverOffsetDoesNotInvalidateSelectedPosition(string invalidOffset)
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            switch (invalidOffset)
            {
                case "rearNaN":
                    context.Input.rearReceiverDistanceFromFrontM = float.NaN;
                    break;
                case "rearInfinity":
                    context.Input.rearReceiverDistanceFromFrontM = float.PositiveInfinity;
                    break;
                case "rearNegative":
                    context.Input.rearReceiverDistanceFromFrontM = -1f;
                    break;
                case "reversedOffsets":
                    context.Input.rearReceiverDistanceFromFrontM = 1f;
                    break;
            }
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.True);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(55f));
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.False);
        }

        [Test]
        public void MissingMassSnapshotPreventsRelativePositionAndHealthyOutput()
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            context.Input.hasCarMasses = false;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.True);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.True);
        }

        [Test]
        public void ZeroReceiverSpacingCanRepresentOneCarWithoutDroppingPosition()
        {
            var context = CreateContext(CreateLine(), 50f, 50f);
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(55f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(55f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void CalculationDoesNotMutateInputOrEarlierPositionSnapshots()
        {
            var initialFront = Position("a", 50f);
            var initialRear = Position("a", 20f);
            var context = CreateContext(CreateLine(), initialFront, initialRear, 30f);
            var frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            var rearTelegram = Telegram("a", TrackAtcTravelDirection.BtoA, "a");
            context.Input.frontTelegram = frontTelegram;
            context.Input.rearTelegram = rearTelegram;
            context.Input.signedSpeedMps = 5f;
            var previousFront = context.State.position.frontPosition;
            var previousRear = context.State.position.rearPosition;
            var firstCar = context.Input.cars[0];
            TrainAtcLogic.Calculate(context);

            Assert.That(context.Input.frontTelegram, Is.SameAs(frontTelegram));
            Assert.That(context.Input.rearTelegram, Is.SameAs(rearTelegram));
            Assert.That(frontTelegram.routeAtoB.atcEdgePath, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(rearTelegram.routeBtoA.atcEdgePath, Is.EqualTo(new[] { "a" }));
            Assert.That(context.Input.frontReceiverDistanceFromFrontM, Is.EqualTo(2f));
            Assert.That(context.Input.rearReceiverDistanceFromFrontM, Is.EqualTo(32f));
            Assert.That(context.Input.cars[0].massKg, Is.EqualTo(firstCar.massKg));
            Assert.That(context.Input.cars[0].centerDistanceFromFrontM, Is.EqualTo(firstCar.centerDistanceFromFrontM));
            Assert.That(initialFront.distanceOnAtcEdgeM, Is.EqualTo(50f));
            Assert.That(initialRear.distanceOnAtcEdgeM, Is.EqualTo(20f));
            Assert.That(previousFront.distanceOnAtcEdgeM, Is.EqualTo(50f));
            Assert.That(previousRear.distanceOnAtcEdgeM, Is.EqualTo(20f));
            Assert.That(context.State.operation.currentPosition, Is.Not.SameAs(context.State.position.frontPosition));
        }

        [TestCase("measurement")]
        [TestCase("speedNaN")]
        [TestCase("speedInfinity")]
        [TestCase("timeNaN")]
        [TestCase("timeInfinity")]
        [TestCase("negativeTime")]
        public void InvalidMeasurementLatchesUnknownUntilExplicitCorrection(string condition)
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            switch (condition)
            {
                case "measurement": context.Input.hasSpeedMeasurement = false; break;
                case "speedNaN": context.Input.signedSpeedMps = float.NaN; break;
                case "speedInfinity": context.Input.signedSpeedMps = float.PositiveInfinity; break;
                case "timeNaN": context.Input.deltaTimeSeconds = float.NaN; break;
                case "timeInfinity": context.Input.deltaTimeSeconds = float.PositiveInfinity; break;
                case "negativeTime": context.Input.deltaTimeSeconds = -1f; break;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.isFrontPositionKnown, Is.False);
            Assert.That(context.State.position.isRearPositionKnown, Is.False);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);

            context.Input.hasSpeedMeasurement = true;
            context.Input.signedSpeedMps = 0f;
            context.Input.deltaTimeSeconds = 1f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(TrainAtcLogic.TryCorrectPosition(context, default, default), Is.False);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(TrainAtcLogic.TryCorrectPosition(context,
                Position("a", 60f), Position("a", 30f)), Is.True);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(60f));
            Assert.That(context.State.operation.isAtcEnabled, Is.True);
        }

        [Test]
        public void ZeroElapsedTimeDoesNotInvalidatePositionDuringReconnection()
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.deltaTimeSeconds = 0f;
            context.Input.hasSpeedMeasurement = false;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(50f));
        }

        [Test]
        public void HugeMovementAroundCycleFailsWithoutLoopingForever()
        {
            var graph = CreateLine();
            graph.atcEdge.Add(Edge("c", "n2", "n0"));
            var context = CreateContext(graph, 50f, 20f);
            context.Input.signedSpeedMps = float.MaxValue;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.IsPositionKnown, Is.False);
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(50f));
        }

        [TestCase(0f, 15f, 5f)]
        [TestCase(10f, 25f, 15f)]
        public void CommonSpanRebindsBothEndsToSelectedBranchBeforeOdometry(
            float signedSpeedMps, float expectedFront, float expectedRear)
        {
            var context = CreateContext(CreateCompositeTurnout(), 15f, 5f, true, "normal");
            context.Input.signedSpeedMps = signedSpeedMps;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.AtoB, "reverse");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("reverse"));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("reverse"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedFront));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedRear));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void PositionAfterBranchDoesNotRebindToOtherBranch()
        {
            var context = CreateContext(CreateCompositeTurnout(), 45f, 30f, true, "normal");
            context.Input.signedSpeedMps = 10f;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.AtoB, "reverse");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("normal"));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("normal"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(55f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void SharedSpanBoundaryCanStillAdoptSelectedEdge()
        {
            var context = CreateContext(CreateCompositeTurnout(), 20f, 20f, true, "normal");
            context.Input.signedSpeedMps = 0f;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.AtoB, "reverse");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("reverse"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(20f));
        }

        [TestCase(false, "normal")]
        [TestCase(true, "reverse")]
        public void ZeroElapsedTimeRebindsWithKeyOffOnlyWhenTelegramIsValid(bool valid, string expectedEdge)
        {
            var context = CreateContext(CreateCompositeTurnout(), 15f, 5f, true, "normal");
            context.Input.deltaTimeSeconds = 0f;
            context.Input.hasSpeedMeasurement = false;
            context.Input.cab.isKeyInserted = false;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.AtoB, "reverse");
            context.Input.frontTelegram.isValid = valid;
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo(expectedEdge));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo(expectedEdge));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(15f));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [TestCase(10f, 125f, 135f)]
        [TestCase(-3f, 138f, 148f)]
        public void RebindingToReversedCompositeCoordinatesPreservesConsistOrientation(
            float signedSpeedMps, float expectedFront, float expectedRear)
        {
            var graph = CreateCompositeTurnout(true);
            var context = CreateContext(graph, 15f, 5f, true, "normal");
            context.Input.signedSpeedMps = signedSpeedMps;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.BtoA, "reverse");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("reverse"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedFront));
            Assert.That(context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedRear));
            Assert.That(context.State.position.frontPosition.frontFacesAtoB, Is.False);
            Assert.That(context.State.position.rearPosition.frontFacesAtoB, Is.False);
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        [Test]
        public void AdjacentCircuitTelegramSelectsTurnoutEntryBeforePositionCrossesBoundary()
        {
            var graph = CreateCompositeTurnout();
            graph.atcEdge.Add(Edge("approach", "start", "common-node"));
            var context = CreateContext(graph, 95f, 50f, true, "approach");
            context.Input.signedSpeedMps = 10f;
            context.Input.frontTelegram = Telegram("reverse", TrackAtcTravelDirection.AtoB, "reverse");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.position.frontPosition.atcEdgeId, Is.EqualTo("reverse"));
            Assert.That(context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.position.rearPosition.atcEdgeId, Is.EqualTo("approach"));
            Assert.That(context.State.position.IsPositionKnown, Is.True);
        }

        private static TrackAtcGraphDefinition CreateCompositeTurnout(bool reverseCompositeCoordinates = false)
        {
            var graph = new TrackAtcGraphDefinition();
            var normal = Edge("normal", "common-node", "normal-node");
            normal.trackCircuitId = "21T";
            normal.lengthM = 120f;
            normal.physicalSpans.Add(new TrackAtcPhysicalSpan
            {
                trackEdgeId = "common", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 20f
            });
            normal.physicalSpans.Add(new TrackAtcPhysicalSpan
            {
                trackEdgeId = "normal-track", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 100f
            });
            graph.atcEdge.Add(normal);

            var reverse = Edge("reverse", "common-node", "reverse-node");
            reverse.trackCircuitId = "21T";
            reverse.lengthM = 150f;
            if (reverseCompositeCoordinates)
            {
                reverse.atcNodeAId = "reverse-node";
                reverse.atcNodeBId = "common-node";
                reverse.physicalSpans.Add(new TrackAtcPhysicalSpan
                {
                    trackEdgeId = "reverse-track", startDistanceOnEdgeM = 130f, endDistanceOnEdgeM = 0f
                });
                reverse.physicalSpans.Add(new TrackAtcPhysicalSpan
                {
                    trackEdgeId = "common", startDistanceOnEdgeM = 20f, endDistanceOnEdgeM = 0f
                });
            }
            else
            {
                reverse.physicalSpans.Add(new TrackAtcPhysicalSpan
                {
                    trackEdgeId = "common", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 20f
                });
                reverse.physicalSpans.Add(new TrackAtcPhysicalSpan
                {
                    trackEdgeId = "reverse-track", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 130f
                });
            }
            graph.atcEdge.Add(reverse);
            return graph;
        }

        private static TrainAtcContext CreateContext(
            TrackAtcGraphDefinition graph, float frontDistance, float rearDistance,
            bool frontFacesAtoB = true, string edgeId = "a")
        {
            var front = Position(edgeId, frontDistance);
            var rear = Position(edgeId, rearDistance);
            front.frontFacesAtoB = frontFacesAtoB;
            rear.frontFacesAtoB = frontFacesAtoB;
            float receiverSpacingM = (frontDistance - rearDistance) * (frontFacesAtoB ? 1f : -1f);
            return CreateContext(graph, front, rear, receiverSpacingM);
        }

        private static TrainAtcContext CreateContext(
            TrackAtcGraphDefinition graph, TrainAtcPosition front, TrainAtcPosition rear, float receiverSpacingM)
        {
            var context = new TrainAtcContext();
            if (graph.maximumOperatingSpeedKmh == 0f)
            {
                graph.maximumOperatingSpeedKmh = 72f;
            }
            foreach (var edge in graph.atcEdge)
            {
                edge.gradientProfiles.Clear();
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = edge.lengthM });
            }
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, front, rear), Is.True);
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput
            {
                isFrontCab = true,
                isKeyInserted = true,
                reverserPosition = ReverserPosition.Forward
            };
            context.Input.hasSpeedMeasurement = true;
            context.Input.hasCarMasses = true;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 30000f, centerDistanceFromFrontM = 10f });
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 30000f, centerDistanceFromFrontM = 210f });
            context.Input.frontReceiverDistanceFromFrontM = 2f;
            context.Input.rearReceiverDistanceFromFrontM = 2f + receiverSpacingM;
            context.Input.hasBrakeSettings = true;
            context.Input.brakeSettings.brakeSubstepCount = 1;
            context.Input.brakeSettings.maximumServiceBrakeStep = 1;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.Add(0.5f);
            context.Input.hasDoorState = true;
            context.Input.areAllDoorsClosed = true;
            context.Input.deltaTimeSeconds = 1f;
            return context;
        }

        private static TrainAtcContext CreateMergingContext()
        {
            var graph = CreateLine();
            graph.atcEdge.Add(Edge("alternative", "other-start", "n1"));
            graph.atcEdge.Add(Edge("c", "n2", "n3"));
            var context = CreateContext(graph, Position("b", 20f), Position("a", 60f), 60f);
            context.Input.frontTelegram = Telegram("b", TrackAtcTravelDirection.AtoB, "b", "c");
            return context;
        }

        private static TrackAtcGraphDefinition CreateLine()
        {
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(Edge("a", "n0", "n1"));
            graph.atcEdge.Add(Edge("b", "n1", "n2"));
            return graph;
        }

        private static TrackAtcGraphDefinition CreateFork()
        {
            var graph = CreateLine();
            graph.atcEdge.Add(Edge("c", "n1", "n3"));
            // 分岐後のb/cは、21Tと同様に同じ回路の代替ATC Edgeとする。
            graph.atcEdge[1].trackCircuitId = "fork";
            graph.atcEdge[2].trackCircuitId = "fork";
            return graph;
        }

        private static TrackAtcGraphEdge Edge(string id, string nodeA, string nodeB)
        {
            return new TrackAtcGraphEdge
            {
                atcEdgeId = id,
                atcNodeAId = nodeA,
                atcNodeBId = nodeB,
                trackCircuitId = id,
                lengthM = 100f
            };
        }

        private static TrainAtcPosition Position(string edgeId, float distance)
        {
            return new TrainAtcPosition { atcEdgeId = edgeId, distanceOnAtcEdgeM = distance, frontFacesAtoB = true };
        }

        private static TrackAtcRouteDefinition Route(params string[] ids)
        {
            return new TrackAtcRouteDefinition { atcEdgeIds = new List<string>(ids) };
        }

        private static TrackCircuitAtcTelegram Telegram(string id, TrackAtcTravelDirection direction, params string[] ids)
        {
            var telegram = new TrackCircuitAtcTelegram { isValid = true };
            var route = new TrackCircuitAtcRouteInfomation
            {
                atcEdgePath = new List<string>(ids),
                stopAtcEdgeId = ids[ids.Length - 1],
                overrunProtectionMode = OverrunProtectionMode.Normal
            };
            if (direction == TrackAtcTravelDirection.AtoB)
            {
                telegram.routeAtoB = route;
            }
            else
            {
                telegram.routeBtoA = route;
            }
            return telegram;
        }
    }
}
