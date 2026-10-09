using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Atc;
using NUnit.Framework;

namespace Nakatetsu.Train.Simulation.Atc.Tests
{
    public sealed class TrainAtcInitializationTests
    {
        [TestCase(0f, "first", 0f)]
        [TestCase(50f, "first", 50f)]
        [TestCase(100f, "second", 0f)]
        [TestCase(150f, "second", 50f)]
        [TestCase(200f, "second", 100f)]
        public void ResolvesNodeADistanceAndSelectsFollowingEdgeAtBoundary(
            float distance, string expectedEdgeId, float expectedDistance)
        {
            var graph = CreateGraph();
            // 定義の並び順にも、通行が許可された方向にも依存しない。
            graph.atcEdge.Reverse();
            graph.atcEdge[0].direction = TrackAtcTravelDirection.BtoA;
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, "track", distance,
                true, out var position), Is.True);
            Assert.That(position.atcEdgeId, Is.EqualTo(expectedEdgeId));
            Assert.That(position.distanceOnAtcEdgeM, Is.EqualTo(expectedDistance));
            Assert.That(position.frontFacesAtoB, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeepsConsistOrientationInAtcEdgeCoordinates(bool frontFacesAtoB)
        {
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(CreateGraph(), "track", 150f,
                frontFacesAtoB, out var position), Is.True);
            Assert.That(position.frontFacesAtoB, Is.EqualTo(frontFacesAtoB));
            Assert.That(position.distanceOnAtcEdgeM, Is.EqualTo(50f));
        }

        [TestCase(-1f)]
        [TestCase(201f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSampleDoesNotProduceInitialPosition(float distance)
        {
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(CreateGraph(), "track", distance,
                true, out var position), Is.False);
            Assert.That(position, Is.Null);
        }

        [Test]
        public void MissingGraphOrTrackEdgeDoesNotProduceInitialPosition()
        {
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(null, "track", 50f,
                true, out _), Is.False);
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(CreateGraph(), "missing", 50f,
                true, out _), Is.False);
        }

        [Test]
        public void OverlappingAtcEdgesDoNotChooseArbitraryPosition()
        {
            var graph = CreateGraph();
            graph.atcEdge[1].physicalSpans[0].startDistanceOnEdgeM = 50f;
            graph.atcEdge[1].lengthM = 150f;
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, "track", 75f,
                true, out var position), Is.False);
            Assert.That(position, Is.Null);
        }

        [Test]
        public void InvalidAtcLengthDoesNotProduceInitialPosition()
        {
            var graph = CreateGraph();
            graph.atcEdge[1].lengthM = 10f;
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, "track", 150f,
                true, out _), Is.False);
        }

        [TestCase("common", 110f, true, 10f, true)]
        [TestCase("common", 120f, false, 20f, false)]
        [TestCase("branch", 250f, true, 70f, false)]
        [TestCase("branch", 200f, false, 120f, true)]
        public void CompositePositionUsesCumulativePhysicalLengthAndSpanOrientation(
            string trackEdgeId, float physicalDistance, bool physicalFrontFacesAtoB,
            float expectedDistance, bool expectedFrontFacesAtoB)
        {
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "joined", lengthM = 120f,
                physicalSpans = new System.Collections.Generic.List<TrackAtcPhysicalSpan>
                {
                    new() { trackEdgeId = "common", startDistanceOnEdgeM = 100f, endDistanceOnEdgeM = 120f },
                    new() { trackEdgeId = "branch", startDistanceOnEdgeM = 300f, endDistanceOnEdgeM = 200f }
                }
            });
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, trackEdgeId, physicalDistance,
                physicalFrontFacesAtoB, out var position), Is.True);
            Assert.That(position.distanceOnAtcEdgeM, Is.EqualTo(expectedDistance));
            Assert.That(position.frontFacesAtoB, Is.EqualTo(expectedFrontFacesAtoB));
        }

        [TestCase("normal")]
        [TestCase("reverse")]
        public void SelectedTelegramEdgeResolvesSharedCommonSpan(string selectedEdgeId)
        {
            var graph = CreateTurnout();
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, "common", 10f,
                true, out _), Is.False);
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(graph, "common", 10f,
                true, out var position, selectedEdgeId), Is.True);
            Assert.That(position.atcEdgeId, Is.EqualTo(selectedEdgeId));
            Assert.That(position.distanceOnAtcEdgeM, Is.EqualTo(10f));
        }

        [Test]
        public void BranchPositionCannotBeMappedToOtherSelectedBranch()
        {
            Assert.That(TrainAtcInitializationLogic.TryResolvePosition(CreateTurnout(), "normal-track", 50f,
                true, out var position, "reverse"), Is.True);
            Assert.That(position.atcEdgeId, Is.EqualTo("normal"));
            Assert.That(position.distanceOnAtcEdgeM, Is.EqualTo(70f));
        }

        private static TrackAtcGraphDefinition CreateTurnout()
        {
            var graph = new TrackAtcGraphDefinition();
            foreach (string id in new[] { "normal", "reverse" })
            {
                graph.atcEdge.Add(new TrackAtcGraphEdge
                {
                    atcEdgeId = id, lengthM = 120f,
                    physicalSpans = new System.Collections.Generic.List<TrackAtcPhysicalSpan>
                    {
                        new() { trackEdgeId = "common", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 20f },
                        new() { trackEdgeId = id + "-track", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 100f }
                    }
                });
            }
            return graph;
        }

        private static TrackAtcGraphDefinition CreateGraph()
        {
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "first", lengthM = 100f,
                physicalSpans = new System.Collections.Generic.List<TrackAtcPhysicalSpan>
                {
                    new() { trackEdgeId = "track", startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 100f }
                }
            });
            graph.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "second", lengthM = 100f,
                physicalSpans = new System.Collections.Generic.List<TrackAtcPhysicalSpan>
                {
                    new() { trackEdgeId = "track", startDistanceOnEdgeM = 100f, endDistanceOnEdgeM = 200f }
                }
            });
            return graph;
        }
    }
}
