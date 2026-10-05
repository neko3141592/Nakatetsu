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
            graph.atcEdge[1].startDistanceOnEdgeM = 50f;
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

        private static TrackAtcGraphDefinition CreateGraph()
        {
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "first", trackEdgeId = "track",
                startDistanceOnEdgeM = 0f, endDistanceOnEdgeM = 100f, lengthM = 100f
            });
            graph.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "second", trackEdgeId = "track",
                startDistanceOnEdgeM = 100f, endDistanceOnEdgeM = 200f, lengthM = 100f
            });
            return graph;
        }
    }
}
