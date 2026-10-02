using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackAtcTelegramTests
    {
        private TrackAtcContext context;

        [SetUp]
        public void SetUp()
        {
            context = new TrackAtcContext();
            context.Input.simulationTimeSeconds = 123d;
            AddEdge("First", "Circuit1", "A", "B");
            AddEdge("Middle", "Circuit1", "B", "C");
            AddEdge("Last", "Circuit2", "C", "D");
        }

        [TestCase(OverrunProtectionMode.None)]
        [TestCase(OverrunProtectionMode.Normal)]
        [TestCase(OverrunProtectionMode.Restricted)]
        public void GroupsSuffixPathsByCircuitAndPreservesStopBoundary(OverrunProtectionMode mode)
        {
            var path = new List<string> { "First", "Middle", "Last" };
            TrackAtcLogic.CreateTrackCircuitAtcTelegram(context, path, mode, false);

            Assert.That(context.Output.telegrams.Count, Is.EqualTo(2));
            Assert.That(context.Output.telegrams["Circuit1"].atcRouteInfomation.Count, Is.EqualTo(2));
            Assert.That(GetRoute("Circuit1", "First").atcEdgePath, Is.EqualTo(path));
            Assert.That(GetRoute("Circuit1", "Middle").atcEdgePath, Is.EqualTo(new[] { "Middle", "Last" }));
            Assert.That(GetRoute("Circuit2", "Last").atcEdgePath, Is.EqualTo(new[] { "Last" }));
            foreach (var telegram in context.Output.telegrams.Values)
            {
                Assert.That(telegram.isValid, Is.True);
                Assert.That(telegram.issuedAtSeconds, Is.EqualTo(123d));
                foreach (var route in telegram.atcRouteInfomation.Values)
                {
                    Assert.That(route.stopAtcEdgeId, Is.EqualTo("Last"));
                    Assert.That(route.overrunProtectionMode, Is.EqualTo(mode));
                }
            }

            path.Clear();
            GetRoute("Circuit1", "First").atcEdgePath.Clear();
            Assert.That(GetRoute("Circuit1", "Middle").atcEdgePath, Is.EqualTo(new[] { "Middle", "Last" }));
        }

        [Test]
        public void SeparatesPathsAndStopEdgesByDirection()
        {
            Create(new[] { "First", "Middle", "Last" });
            Create(new[] { "Last", "Middle", "First" });
            var telegram = context.Output.telegrams["Circuit1"];
            Assert.That(telegram.isValid, Is.True);
            Assert.That(telegram.atcRouteInfomation.Count, Is.EqualTo(4));
            var reverse = telegram.atcRouteInfomation[("First", TrackAtcTravelDirection.BtoA)];
            Assert.That(reverse.stopAtcEdgeId, Is.EqualTo("First"));
            Assert.That(reverse.atcEdgePath, Is.EqualTo(new[] { "First" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailureInvalidatesWholeCircuitRegardlessOfCalculationOrder(bool failedFirst)
        {
            if (failedFirst)
            {
                Create(new[] { "First" }, true);
                Create(new[] { "First", "Middle", "Last" });
            }
            else
            {
                Create(new[] { "First", "Middle", "Last" });
                Create(new[] { "First" }, true);
            }

            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.False);
            Assert.That(context.Output.telegrams["Circuit2"].isValid, Is.True);

            context.Output.telegrams.Clear();
            context.Input.simulationTimeSeconds = 124d;
            Create(new[] { "First", "Middle", "Last" });
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.True);
            Assert.That(context.Output.telegrams["Circuit1"].issuedAtSeconds, Is.EqualTo(124d));
        }

        [Test]
        public void FailedPathInvalidatesEveryAffectedCircuit()
        {
            Create(new[] { "First", "Middle", "Last" }, true);
            foreach (var telegram in context.Output.telegrams.Values)
            {
                Assert.That(telegram.isValid, Is.False);
                Assert.That(telegram.atcRouteInfomation, Is.Empty);
            }
        }

        [TestCase(TrackAtcTravelDirection.AtoB)]
        [TestCase(TrackAtcTravelDirection.BtoA)]
        public void SingleBlockUsesItsConfiguredDirection(TrackAtcTravelDirection direction)
        {
            var edge = context.Workspace.atcEdgesById["First"];
            edge.controlKind = TrackAtcEdgeControlKind.Block;
            edge.direction = direction;
            Create(new[] { "First" });
            var telegram = context.Output.telegrams["Circuit1"];
            Assert.That(telegram.isValid, Is.True);
            Assert.That(telegram.atcRouteInfomation[("First", direction)].stopAtcEdgeId,
                Is.EqualTo("First"));
        }

        [Test]
        public void UnknownSingleEdgeDirectionDoesNotProduceValidTelegram()
        {
            Create(new[] { "First" });
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidClockDoesNotProduceValidTelegram(double time)
        {
            context.Input.simulationTimeSeconds = time;
            Create(new[] { "First", "Middle", "Last" });
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.False);
            Assert.That(context.Output.telegrams["Circuit2"].isValid, Is.False);
        }

        [TestCase("disconnected")]
        [TestCase("reversed")]
        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("blockDirection")]
        public void InvalidPathDoesNotProduceValidTelegram(string kind)
        {
            var path = new List<string> { "First", "Middle", "Last" };
            if (kind == "disconnected")
                context.Workspace.atcEdgesById["Middle"].atcNodeAId = "Other";
            if (kind == "reversed")
                context.Workspace.atcEdgesById["Last"].atcNodeAId = "B";
            if (kind == "missing")
                context.Workspace.atcEdgesById.Remove("Middle");
            if (kind == "duplicate")
                path.Add("First");
            if (kind == "blockDirection")
            {
                var edge = context.Workspace.atcEdgesById["Middle"];
                edge.controlKind = TrackAtcEdgeControlKind.Block;
                edge.direction = TrackAtcTravelDirection.BtoA;
            }
            Create(path);
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.False);
            Assert.That(context.Output.telegrams["Circuit2"].isValid, Is.False);
        }

        [Test]
        public void CalculatePreservesTerminalProtectionMode()
        {
            context.Workspace.atcNodesById.Add("D", new TrackAtcGraphNode
            {
                atcNodeId = "D", connectedAtcEdgeIds = new List<string> { "Last" }
            });
            context.Workspace.atcRoutesById.Add("AtcRoute", new TrackAtcRouteDefinition
            {
                atcRouteId = "AtcRoute", interlockingRouteId = "InterlockingRoute",
                atcEdgeIds = new List<string> { "First", "Middle", "Last" }
            });
            context.Input.RoutesById.Add("InterlockingRoute", new TrackAtcRouteInput
            {
                ProceedAllowed = true, PathEstablished = true, RouteLocked = true,
                OverrunMode = OverrunProtectionMode.Restricted
            });
            context.Input.OccupiedByCircuitId.Add("Circuit2", false);

            TrackAtcLogic.CalculateNextEdge(context, context.Workspace.atcEdgesById["First"]);
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.True);
            Assert.That(GetRoute("Circuit1", "First").overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.Restricted));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CalculateInitializesGraphAndDoesNotRecalculateTerminal(bool terminalFirst)
        {
            var graph = CreateGraph();
            graph.routes.Add(new TrackAtcRouteDefinition
            {
                atcRouteId = "AtcRoute", interlockingRouteId = "Route",
                atcEdgeIds = new List<string> { "First", "Middle", "Last" }
            });
            if (terminalFirst)
            {
                graph.atcEdge.Reverse();
            }
            context.Input.RoutesById.Add("Route", new TrackAtcRouteInput
            {
                ProceedAllowed = true, PathEstablished = true, RouteLocked = true,
                OverrunMode = OverrunProtectionMode.Restricted
            });
            context.Input.OccupiedByCircuitId.Add("Circuit2", false);
            SetGraph(graph);
            context.Workspace.atcEdgesById.Clear();

            TrackAtcLogic.Calculate(context, 0.1f);

            Assert.That(context.Workspace.atcEdgesById.Count, Is.EqualTo(3));
            Assert.That(context.Workspace.atcNodesById.Count, Is.EqualTo(4));
            Assert.That(context.Workspace.atcRoutesById.Count, Is.EqualTo(1));
            Assert.That(context.Workspace.visitedAtcEdgeIds, Is.EquivalentTo(new[] { "First", "Middle", "Last" }));
            Assert.That(context.Output.telegrams["Circuit1"].isValid, Is.True);
            Assert.That(context.Output.telegrams["Circuit2"].isValid, Is.True);
            Assert.That(GetRoute("Circuit2", "Last").overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.Restricted));

            var circuits = new TrackCircuitSimulationState();
            circuits.SetAtcTelegrams(context.Output.telegrams);
            Assert.That(circuits.TryGetAtcTelegram("Circuit1", out var telegram), Is.True);
            Assert.That(telegram.atcRouteInfomation[("First", TrackAtcTravelDirection.AtoB)].atcEdgePath,
                Is.EqualTo(new[] { "First", "Middle", "Last" }));
        }

        [Test]
        public void NextUpdateRemovesOldGraphAndPublishedTelegrams()
        {
            var graph = CreateGraph();
            foreach (var edge in graph.atcEdge)
            {
                edge.controlKind = TrackAtcEdgeControlKind.Block;
                edge.direction = TrackAtcTravelDirection.AtoB;
            }
            context.Input.OccupiedByCircuitId.Add("Circuit2", false);
            SetGraph(graph);
            TrackAtcLogic.Calculate(context, 0.1f);
            var circuits = new TrackCircuitSimulationState();
            circuits.SetAtcTelegrams(context.Output.telegrams);
            Assert.That(circuits.TryGetAtcTelegram("Circuit1", out var previous), Is.True);
            Assert.That(previous.isValid, Is.True);

            graph.atcEdge.RemoveRange(0, 2);
            graph.atcNode.RemoveAll(node => node.atcNodeId == "A" || node.atcNodeId == "B");
            graph.atcNode.Find(node => node.atcNodeId == "C").connectedAtcEdgeIds.Remove("Middle");
            context.Input.simulationTimeSeconds = 124d;
            TrackAtcLogic.Calculate(context, 0.1f);
            circuits.SetAtcTelegrams(context.Output.telegrams);

            Assert.That(context.Workspace.atcEdgesById.Count, Is.EqualTo(1));
            Assert.That(context.Workspace.visitedAtcEdgeIds, Is.EquivalentTo(new[] { "Last" }));
            Assert.That(circuits.TryGetAtcTelegram("Circuit1", out _), Is.False);
            Assert.That(circuits.TryGetAtcTelegram("Circuit2", out var updated), Is.True);
            Assert.That(updated.isValid, Is.True);
            Assert.That(updated.issuedAtSeconds, Is.EqualTo(124d));

            SetGraph(null);
            TrackAtcLogic.Calculate(context, 0.1f);
            circuits.SetAtcTelegrams(context.Output.telegrams);
            Assert.That(circuits.TryGetAtcTelegram("Circuit2", out _), Is.False);
            Assert.That(context.Workspace.atcEdgesById, Is.Empty);
        }

        [Test]
        public void FailedCalculationIsDeliveredAsInvalidRatherThanMissing()
        {
            var graph = CreateGraph();
            foreach (var edge in graph.atcEdge)
            {
                edge.controlKind = TrackAtcEdgeControlKind.Block;
                edge.direction = TrackAtcTravelDirection.Unspecified;
            }
            SetGraph(graph);
            TrackAtcLogic.Calculate(context, 0.1f);
            var circuits = new TrackCircuitSimulationState();
            circuits.SetAtcTelegrams(context.Output.telegrams);

            Assert.That(circuits.TryGetAtcTelegram("Circuit1", out var telegram), Is.True);
            Assert.That(telegram.isValid, Is.False);
            Assert.That(telegram.atcRouteInfomation, Is.Empty);
            Assert.That(circuits.TryGetAtcTelegram("Unknown", out _), Is.False);
            Assert.That(circuits.TryGetAtcTelegram(null, out _), Is.False);

            // 停止・無効化時に受信電文を取り下げる。
            circuits.SetAtcTelegrams(null);
            Assert.That(circuits.TryGetAtcTelegram("Circuit1", out _), Is.False);
        }

        [Test]
        public void BrokenGraphCannotLeavePreviousOutput()
        {
            Create(new[] { "First", "Middle", "Last" });
            var graph = CreateGraph();
            graph.atcNode.Clear();
            SetGraph(graph);
            TrackAtcLogic.Calculate(context, 0.1f);
            Assert.That(context.Output.telegrams, Is.Empty);
        }

        private TrackAtcGraphDefinition CreateGraph()
        {
            var graph = new TrackAtcGraphDefinition
            {
                atcEdge = new List<TrackAtcGraphEdge>(context.Workspace.atcEdgesById.Values)
            };
            var nodes = new Dictionary<string, TrackAtcGraphNode>();
            foreach (var edge in graph.atcEdge)
            {
                foreach (string nodeId in new[] { edge.atcNodeAId, edge.atcNodeBId })
                {
                    if (!nodes.TryGetValue(nodeId, out var node))
                    {
                        node = new TrackAtcGraphNode { atcNodeId = nodeId };
                        nodes.Add(nodeId, node);
                        graph.atcNode.Add(node);
                    }
                    node.connectedAtcEdgeIds.Add(edge.atcEdgeId);
                }
            }
            return graph;
        }

        private void SetGraph(TrackAtcGraphDefinition graph) =>
            typeof(TrackAtcContext).GetProperty(nameof(TrackAtcContext.Graph)).SetValue(context, graph);

        private void Create(IEnumerable<string> path, bool failed = false) =>
            TrackAtcLogic.CreateTrackCircuitAtcTelegram(context, new List<string>(path), OverrunProtectionMode.None, failed);

        private TrackCircuitAtcRouteInfomation GetRoute(string circuitId, string edgeId) =>
            context.Output.telegrams[circuitId].atcRouteInfomation[(edgeId, TrackAtcTravelDirection.AtoB)];

        private void AddEdge(string id, string circuitId, string nodeA, string nodeB)
        {
            context.Workspace.atcEdgesById.Add(id, new TrackAtcGraphEdge
            {
                atcEdgeId = id, trackCircuitId = circuitId, atcNodeAId = nodeA, atcNodeBId = nodeB,
                controlKind = TrackAtcEdgeControlKind.Interlocking
            });
        }
    }
}
