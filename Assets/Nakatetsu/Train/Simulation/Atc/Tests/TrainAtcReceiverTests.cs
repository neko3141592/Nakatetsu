using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Circuit;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Simulation.Atc;
using NUnit.Framework;

namespace Nakatetsu.Train.Simulation.Atc.Tests
{
    public sealed class TrainAtcReceiverTests
    {
        [TestCase(0f, "a")]
        [TestCase(99f, "a")]
        [TestCase(100f, "b")]
        [TestCase(200f, "b")]
        public void ResolvesCircuitUsingEdgeDistance(float distance, string expected)
        {
            Assert.That(TrainAtcReceiverLogic.TryResolveCircuitId(CreateGraph(), "track", distance,
                out string circuitId), Is.True);
            Assert.That(circuitId, Is.EqualTo(expected));
        }

        [Test]
        public void ReversedGeometryUsesNodeADistance()
        {
            var graph = CreateGraph(true);
            Assert.That(TrainAtcReceiverLogic.TryResolveCircuitId(graph, "track", 20f, out var id), Is.True);
            Assert.That(id, Is.EqualTo("b"));
            Assert.That(TrainAtcReceiverLogic.TryResolveCircuitId(graph, "track", 100f, out id), Is.True);
            Assert.That(id, Is.EqualTo("a"));
        }

        [TestCase(-1f)]
        [TestCase(201f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPositionDoesNotChooseCircuit(float distance)
        {
            Assert.That(TrainAtcReceiverLogic.TryResolveCircuitId(CreateGraph(), "track", distance,
                out var id), Is.False);
            Assert.That(id, Is.Null);
        }

        [Test]
        public void OverlappingCircuitsDoNotChooseArbitraryTelegram()
        {
            var graph = CreateGraph();
            graph.TryGetCircuit("b", out var circuit);
            circuit.sections[0].startDistanceOnGeometryM = 1020f;
            Assert.That(TrainAtcReceiverLogic.TryResolveCircuitId(graph, "track", 60f, out var id), Is.False);
            Assert.That(id, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReceivesWholeTelegramIncludingInvalidTelegram(bool valid)
        {
            var context = NewReceiver();
            var source = NewTelegram(valid);
            var circuits = new TrackCircuitSimulationState();
            circuits.SetAtcTelegrams(new Dictionary<string, TrackCircuitAtcTelegram> { ["a"] = source });
            TrainAtcReceiverLogic.Calculate(context, CreateGraph(), circuits);
            source.routeAtoB.atcEdgePath.Clear();
            source.routeBtoA = null;

            Assert.That(TrainAtcReceiverLogic.TryGetTelegram(context, out var received), Is.True);
            Assert.That(received.isValid, Is.EqualTo(valid));
            Assert.That(received.issuedAtSeconds, Is.EqualTo(43200.125d));
            Assert.That(received.routeAtoB.atcEdgePath, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(received.routeBtoA.atcEdgePath, Is.EqualTo(new[] { "one" }));
            received.routeAtoB.atcEdgePath.Clear();
            received.routeBtoA = null;
            Assert.That(context.Output.telegram.routeAtoB.atcEdgePath.Count, Is.EqualTo(2));
            Assert.That(context.Output.telegram.routeBtoA, Is.Not.Null);

            circuits.SetAtcTelegrams(null);
            TrainAtcReceiverLogic.Calculate(context, CreateGraph(), circuits);
            Assert.That(TrainAtcReceiverLogic.TryGetTelegram(context, out _), Is.False);
        }

        [Test]
        public void MissingPositionClearsPreviousTelegram()
        {
            var context = NewReceiver();
            context.Output.telegram = NewTelegram();
            context.Input.hasEdgePosition = false;
            TrainAtcReceiverLogic.Calculate(context);
            Assert.That(context.Output.hasEdgePosition, Is.False);
            Assert.That(context.Output.telegram, Is.Null);
        }

        public static TrackCircuitAtcTelegram NewTelegram(bool valid = true)
        {
            var telegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 43200.125d, isValid = valid };
            telegram.routeAtoB = new TrackCircuitAtcRouteInfomation
            {
                atcEdgePath = new List<string> { "one", "two" },
                stopAtcEdgeId = "two",
                overrunProtectionMode = OverrunProtectionMode.Restricted
            };
            telegram.routeBtoA = new TrackCircuitAtcRouteInfomation
            {
                atcEdgePath = new List<string> { "one" }, stopAtcEdgeId = "one"
            };
            return telegram;
        }

        private static TrainAtcReceiverContext NewReceiver()
        {
            var context = new TrainAtcReceiverContext();
            context.Input.hasEdgePosition = true;
            context.Input.edgeId = "track";
            context.Input.distanceOnEdgeM = 20f;
            return context;
        }

        private static TrackGraphContext CreateGraph(bool reversed = false)
        {
            float start = 1000f;
            float end = 1100f;
            if (reversed) { start = 1100f; end = 1000f; }
            var definition = new TrackGraphDefinition();
            definition.edges.Add(new TrackEdgeDefinition
            {
                edgeId = "track",
                distanceMap = new List<TrackEdgeDistanceSample>
                {
                    new() { distanceOnEdgeM = 0f, distanceOnGeometryM = start },
                    new() { distanceOnEdgeM = 200f, distanceOnGeometryM = end }
                }
            });
            definition.circuits.Add(new TrackCircuitDefinition
            {
                circuitId = "a", sections = new List<TrackCircuitSection>
                {
                    new() { edgeId = "track", startDistanceOnGeometryM = 1000f, endDistanceOnGeometryM = 1050f }
                }
            });
            definition.circuits.Add(new TrackCircuitDefinition
            {
                circuitId = "b", sections = new List<TrackCircuitSection>
                {
                    new() { edgeId = "track", startDistanceOnGeometryM = 1050f, endDistanceOnGeometryM = 1100f }
                }
            });
            var graph = new TrackGraphContext();
            Assert.That(graph.TryBuildLookups(definition, out var error), Is.True, error);
            return graph;
        }
    }
}
