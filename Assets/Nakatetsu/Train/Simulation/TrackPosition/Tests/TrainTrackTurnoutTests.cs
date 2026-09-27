using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.TrackPosition.Tests
{
    public sealed class TrainTrackTurnoutTests
    {
        private const string AssetPath = "Assets/Nakatetsu/Track/NtLine/Data/NtLineGraph.asset";
        private const string ConnectionId = "connection-preview-tail-start";

        [Test]
        public void NtLineTurnoutHasTwoContinuousRoutes()
        {
            var graph = LoadGraph();
            Assert.That(graph.TryGetEdge("preview-tail-straight", out var normal), Is.True);
            Assert.That(graph.TryGetEdge("preview-tail-diverge", out var reverse), Is.True);
            Assert.That(normal.LengthM, Is.GreaterThan(1000f));
            Assert.That(reverse.LengthM, Is.GreaterThan(1000f));

            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, normal.edgeId, 0f, out var normalStart), Is.True);
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, reverse.edgeId, 0f, out var reverseStart), Is.True);
            Assert.That(Vector3.Distance(normalStart.Position, reverseStart.Position), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(normalStart.Tangent, reverseStart.Tangent), Is.LessThan(0.01f));
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, normal.edgeId, 200f, out var normalLater), Is.True);
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, reverse.edgeId, 200f, out var reverseLater), Is.True);
            Assert.That(Vector3.Distance(normalLater.Position, reverseLater.Position), Is.EqualTo(3.5f).Within(0.02f));
        }

        [TestCase(TrackSwitchPosition.Normal, "preview-tail-straight")]
        [TestCase(TrackSwitchPosition.Reverse, "preview-tail-diverge")]
        public void TenCarTrainUsesCurrentConfirmedRoute(
            TrackSwitchPosition position, string expectedEdgeId)
        {
            var graph = LoadGraph();
            var connections = new TrackConnectionContext();
            Assert.That(TrackConnectionLogic.TryInitialize(connections, graph, position, out var error), Is.True, error);
            Assert.That(graph.TryGetEdge("preview-easement-out", out var approach), Is.True);

            var train = NewTrain(approach.edgeId, approach.LengthM - 30f);
            bool Resolve(string nodeId, string incomingId, out string nextId) =>
                TrackConnectionLogic.TryResolveNextEdge(connections, nodeId, incomingId, out nextId, out _);

            train.Input.signedDisplacementM = 35f;
            TrainTrackPositionLogic.Calculate(train, graph, Resolve);
            Assert.That(train.State.currentEdgeId, Is.EqualTo(expectedEdgeId));
            Assert.That(TrainTrackSamplingLogic.TryGetTrackSample(train, graph, Resolve,
                0, 0f, out var firstCar), Is.True);
            Assert.That(firstCar.EdgeId, Is.EqualTo(expectedEdgeId));
            Assert.That(TrainTrackSamplingLogic.TryGetTrackSample(train, graph, Resolve,
                9, 0f, out var lastCar), Is.True);
            Assert.That(lastCar.EdgeId, Is.Not.EqualTo(expectedEdgeId));
            Assert.That(train.Output.TryGetBogies(0, out _, out _), Is.True);
            Assert.That(train.Output.OccupiedEdges[0].EdgeId, Is.EqualTo(expectedEdgeId));
        }

        [Test]
        public void UnconfirmedSwitchStopsAtNode()
        {
            var graph = LoadGraph();
            var connections = new TrackConnectionContext();
            Assert.That(TrackConnectionLogic.TryInitialize(connections, graph, TrackSwitchPosition.Normal,
                out var error), Is.True, error);
            Assert.That(TrackConnectionLogic.TryRequestPosition(connections, ConnectionId,
                TrackSwitchPosition.Reverse, out error), Is.True, error);
            Assert.That(graph.TryGetEdge("preview-easement-out", out var approach), Is.True);
            var train = NewTrain(approach.edgeId, approach.LengthM - 30f);
            bool Resolve(string nodeId, string incomingId, out string nextId) =>
                TrackConnectionLogic.TryResolveNextEdge(connections, nodeId, incomingId, out nextId, out _);

            train.Input.signedDisplacementM = 40f;
            TrainTrackPositionLogic.Calculate(train, graph, Resolve);
            Assert.That(train.State.currentEdgeId, Is.EqualTo(approach.edgeId));
            Assert.That(train.State.distanceOnEdgeM, Is.EqualTo(approach.LengthM).Within(0.001f));
        }

        private static TrackGraphContext LoadGraph()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null);
            var graph = new TrackGraphContext();
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryCompile(asset.Definition, graph, errors), Is.True,
                string.Join("\n", errors));
            return graph;
        }

        private static TrainTrackPositionContext NewTrain(string edgeId, float distanceM)
        {
            var train = new TrainTrackPositionContext();
            TrainTrackSamplingLogic.ConfigureLayout(train,
                new float[] { 20f, 20f, 20f, 20f, 20f, 20f, 20f, 20f, 20f, 20f },
                new float[] { 12f, 12f, 12f, 12f, 12f, 12f, 12f, 12f, 12f, 12f });
            train.State.currentEdgeId = edgeId;
            train.State.distanceOnEdgeM = distanceM;
            train.State.frontFacesAtoB = true;
            return train;
        }
    }
}
