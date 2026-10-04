using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcPositionTests
    {
        [TestCase(10f, true, 55f, 25f)]
        [TestCase(-10f, true, 45f, 15f)]
        [TestCase(10f, false, 45f, 15f)]
        [TestCase(-10f, false, 55f, 25f)]
        public void IntegratesSignedMeasuredSpeedForBothEnds(
            float speed, bool frontFacesAtoB, float expectedFront, float expectedRear)
        {
            var context = CreateContext(CreateLine(), 50f, 20f, frontFacesAtoB);
            context.Input.deltaTimeSeconds = 0.5f;
            context.Input.signedSpeedMps = speed;
            // レバーサ位置や運転台の前後は実測の移動方向を変えない。
            context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedFront));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedRear));
            Assert.That(context.State.currentPosition, Is.EqualTo(context.State.frontPosition));
            Assert.That(context.State.isPositionKnown, Is.True);
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
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(60f));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.isPositionKnown, Is.True);
            Assert.That(context.State.isAtcEnabled, Is.False);

            context.Input.hasCabState = true;
            context.Input.cab.carIndex = 7;
            context.Input.cab.isFrontCab = false;
            context.Input.cab.isKeyInserted = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.isAtcEnabled, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UniqueConnectionIsTraversedWithoutValidTelegram(bool invalidTelegram)
        {
            var context = CreateContext(CreateLine(), 95f, 20f);
            context.Input.signedSpeedMps = 10f;
            if (invalidTelegram) context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = false };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(30f));
            Assert.That(context.State.isHealthy, Is.True);
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
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.frontPosition.frontFacesAtoB, Is.False);
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(85f));
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
        }

        [Test]
        public void ReverserDirectionIsKeptWhileMeasuredSpeedMovesOppositeDirection()
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.signedSpeedMps = -10f;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(10f));
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.AtoB));
        }

        [Test]
        public void BackwardTravelUsesMeasuredDirectionAndCarriesDistanceAcrossEdges()
        {
            var context = CreateContext(CreateLine(), 5f, 50f, true, "b");
            context.Input.signedSpeedMps = -10f;
            context.Input.frontTelegram = Telegram("b", TrackAtcTravelDirection.BtoA, "b", "a");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.frontPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(40f));
        }

        [Test]
        public void ExactBoundarySelectsNextEdgeAndTerminalEndpointRemainsKnown()
        {
            var context = CreateContext(CreateLine(), 95f, 20f);
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.Zero);

            context = CreateContext(CreateLine(), 95f, 20f, true, "b");
            context.Input.signedSpeedMps = 5f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(100f));
            Assert.That(context.State.isPositionKnown, Is.True);
            context.Input.signedSpeedMps = 1f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.False);
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
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(5f));
            Assert.That(context.State.isPositionKnown, Is.True);
        }

        [Test]
        public void AcceptedPatternResolvesSelectedReceiverBranchDuringSignalGrace()
        {
            var graph = CreateFork();
            graph.maximumOperatingSpeedKmh = 72f;
            foreach (var edge in graph.atcEdge)
            {
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = edge.lengthM });
            }
            var context = CreateContext(graph, 95f, 20f);
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            context.Input.hasCarMasses = true;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 30000f, centerDistanceFromFrontM = 10f });
            context.Input.frontReceiverDistanceFromFrontM = 10f;
            context.Input.rearReceiverDistanceFromFrontM = 10f;
            context.Input.hasBrakeSettings = true;
            context.Input.brakeSettings.brakeSubstepCount = 1;
            context.Input.brakeSettings.maximumServiceBrakeStep = 1;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.Add(0.5f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "c");
            context.Input.frontTelegram.atcRouteInfomation[("a", TrackAtcTravelDirection.AtoB)]
                .overrunProtectionMode = OverrunProtectionMode.Normal;
            context.Input.deltaTimeSeconds = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.True);

            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.5f;
            context.Input.signedSpeedMps = 12f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(1f));
            Assert.That(context.State.rearPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(26f));
            Assert.That(context.State.isPositionKnown, Is.True);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.5f));
            Assert.That(context.Output.distanceOnPathM, Is.EqualTo(101f));
            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.Output.brake.isEmergency, Is.False);
        }

        [Test]
        public void BothEndsUseTheirOwnTelegramEvenWhenCabIsUnavailable()
        {
            var context = CreateContext(CreateFork(), 95f, 96f);
            context.Input.hasCabState = false;
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            context.Input.rearTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "c");
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.rearPosition.atcEdgeId, Is.EqualTo("c"));
            Assert.That(context.State.isPositionKnown, Is.True);
            Assert.That(context.State.hasCurrentPosition, Is.False);
        }

        [Test]
        public void CarriesTelegramPathAcrossSeveralEdgesInOneUpdate()
        {
            var graph = CreateFork();
            graph.atcEdge.Add(Edge("d", "n2", "n4"));
            graph.atcEdge.Add(Edge("e", "n2", "n5"));
            var context = CreateContext(graph, 95f, 96f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b", "d");
            context.Input.rearTelegram = context.Input.frontTelegram.Clone();
            context.Input.signedSpeedMps = 120f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("d"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(15f));
            Assert.That(context.State.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(16f));
            Assert.That(context.State.isPositionKnown, Is.True);
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
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("b"));
            Assert.That(context.State.isPositionKnown, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosedRouteDefinitionAlsoResolvesBackwardTravel(bool frontFacesAtoB)
        {
            var graph = CreateFork();
            graph.routes.Add(Route("a", "b"));
            var context = CreateContext(graph, 5f, 50f, frontFacesAtoB, "b");
            context.Input.signedSpeedMps = 10f;
            if (frontFacesAtoB) context.Input.signedSpeedMps = -10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("a"));
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(95f));
            Assert.That(context.State.frontPosition.frontFacesAtoB, Is.EqualTo(frontFacesAtoB));
            Assert.That(context.State.isPositionKnown, Is.True);
        }

        [Test]
        public void TelegramForOppositeDirectionDoesNotResolveAnAmbiguousFork()
        {
            var context = CreateContext(CreateFork(), 95f, 20f);
            context.Input.frontTelegram = Telegram("a", TrackAtcTravelDirection.BtoA, "a", "b");
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.EqualTo("a"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AmbiguousBranchKeepsBothLastKnownPositionsAndLatchesFailure(bool multipleRoutes)
        {
            var graph = CreateFork();
            if (multipleRoutes)
            {
                graph.routes.Add(Route("a", "b"));
                graph.routes.Add(Route("a", "c"));
            }
            var context = CreateContext(graph, 20f, 95f);
            var front = context.State.frontPosition;
            var rear = context.State.rearPosition;
            context.Input.signedSpeedMps = 10f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.frontPosition, Is.EqualTo(front));
            Assert.That(context.State.rearPosition, Is.EqualTo(rear));
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(context.State.hasCurrentPosition, Is.False);
            Assert.That(context.State.isHealthy, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);

            context.Input.rearTelegram = Telegram("a", TrackAtcTravelDirection.AtoB, "a", "b");
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(context.State.isHealthy, Is.False);
            Assert.That(context.State.frontPosition, Is.EqualTo(front));
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
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(context.State.isHealthy, Is.False);

            context.Input.hasSpeedMeasurement = true;
            context.Input.signedSpeedMps = 0f;
            context.Input.deltaTimeSeconds = 1f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(TrainAtcLogic.TryCorrectPosition(context, default, default), Is.False);
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(TrainAtcLogic.TryCorrectPosition(context,
                Position("a", 60f), Position("a", 30f)), Is.True);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.True);
            Assert.That(context.State.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(60f));
            Assert.That(context.State.isAtcEnabled, Is.True);
        }

        [Test]
        public void ZeroElapsedTimeDoesNotInvalidatePositionDuringReconnection()
        {
            var context = CreateContext(CreateLine(), 50f, 20f);
            context.Input.deltaTimeSeconds = 0f;
            context.Input.hasSpeedMeasurement = false;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.True);
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(50f));
        }

        [Test]
        public void HugeMovementAroundCycleFailsWithoutLoopingForever()
        {
            var graph = CreateLine();
            graph.atcEdge.Add(Edge("c", "n2", "n0"));
            var context = CreateContext(graph, 50f, 20f);
            context.Input.signedSpeedMps = float.MaxValue;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isPositionKnown, Is.False);
            Assert.That(context.State.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(50f));
        }

        private static TrainAtcContext CreateContext(
            TrackAtcGraphDefinition graph, float frontDistance, float rearDistance,
            bool frontFacesAtoB = true, string edgeId = "a")
        {
            var context = new TrainAtcContext();
            var front = Position(edgeId, frontDistance);
            var rear = Position(edgeId, rearDistance);
            front.frontFacesAtoB = frontFacesAtoB;
            rear.frontFacesAtoB = frontFacesAtoB;
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, front, rear), Is.True);
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput
            {
                isFrontCab = true,
                isKeyInserted = true,
                reverserPosition = ReverserPosition.Forward
            };
            context.Input.hasSpeedMeasurement = true;
            context.Input.deltaTimeSeconds = 1f;
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
            return graph;
        }

        private static TrackAtcGraphEdge Edge(string id, string nodeA, string nodeB)
        {
            return new TrackAtcGraphEdge { atcEdgeId = id, atcNodeAId = nodeA, atcNodeBId = nodeB, lengthM = 100f };
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
            telegram.atcRouteInfomation.Add((id, direction),
                new TrackCircuitAtcRouteInfomation { atcEdgePath = new List<string>(ids) });
            return telegram;
        }
    }
}
