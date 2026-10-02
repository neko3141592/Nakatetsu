using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackAtcRouteResolutionTests
    {
        private TrackAtcContext context;
        private TrackAtcGraphEdge first;
        private TrackAtcGraphEdge shared;
        private TrackAtcGraphEdge middle;
        private TrackAtcGraphEdge last;
        private TrackAtcRouteDefinition arrival;
        private TrackAtcRouteDefinition departure;

        [SetUp]
        public void SetUp()
        {
            context = new TrackAtcContext();
            context.Input.simulationTimeSeconds = 120d;
            first = AddEdge("First", "A", "B");
            shared = AddEdge("Shared", "B", "C");
            middle = AddEdge("Middle", "C", "D");
            last = AddEdge("Last", "D", "E");
            arrival = AddRoute("Arrival", "First", "Shared");
            departure = AddRoute("Departure", "Shared", "Middle", "Last");
        }

        [Test]
        public void FindsRouteFromMiddleAfterEntry()
        {
            context.Input.RoutesById[departure.interlockingRouteId].ProceedAllowed = false;
            Assert.That(Resolve(middle, null, -1, out var next, out var route, out int index), Is.True);
            Assert.That(next, Is.SameAs(last));
            Assert.That(route, Is.SameAs(departure));
            Assert.That(index, Is.EqualTo(2));
        }

        [Test]
        public void ContinuesSelectedRouteAfterEntry()
        {
            context.Input.RoutesById[departure.interlockingRouteId].ProceedAllowed = false;
            Assert.That(Resolve(shared, departure, 0, out var next, out var route, out int index), Is.True);
            Assert.That(next, Is.SameAs(middle));
            Assert.That(route, Is.SameAs(departure));
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void SwitchesAtSharedLastEdge()
        {
            Assert.That(Resolve(shared, arrival, 1, out var next, out var route, out int index), Is.True);
            Assert.That(next, Is.SameAs(middle));
            Assert.That(route, Is.SameAs(departure));
            Assert.That(index, Is.EqualTo(1));
        }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void SuccessorRequiresEntryPermission(bool proceed, bool established, bool cancelled)
        {
            var state = context.Input.RoutesById[departure.interlockingRouteId];
            state.ProceedAllowed = proceed;
            state.PathEstablished = established;
            state.CancelPending = cancelled;
            AssertStopped(shared, arrival, 1);
            AssertUnresolved(shared);
        }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void InteriorRequiresEstablishedLockedRoute(bool established, bool locked, bool cancelled)
        {
            var state = context.Input.RoutesById[departure.interlockingRouteId];
            state.PathEstablished = established;
            state.RouteLocked = locked;
            state.CancelPending = cancelled;
            AssertUnresolved(middle);
            AssertStopped(middle, departure, 1);
        }

        [Test]
        public void CancelledArrivalCanContinueUsingPermittedDeparture()
        {
            context.Input.RoutesById[arrival.interlockingRouteId].CancelPending = true;
            Assert.That(Resolve(shared, arrival, 1, out var next, out var route, out int index), Is.True);
            Assert.That(next, Is.SameAs(middle));
            Assert.That(route, Is.SameAs(departure));
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotReverseOnSharedEdge()
        {
            context.Workspace.atcRoutesById.Remove(departure.atcRouteId);
            AddRoute("Reverse", "Shared", "First");
            AssertStopped(shared, arrival, 1);
        }

        [Test]
        public void StopsAtLastEdgeWithoutSuccessor()
        {
            context.Workspace.atcRoutesById.Remove(departure.atcRouteId);
            AssertStopped(shared, arrival, 1);
            AssertUnresolved(shared);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExitsToBlockAndClearsRoute(bool reversedEndpoints)
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            if (reversedEndpoints)
            {
                block.atcNodeAId = "F";
                block.atcNodeBId = "E";
                block.direction = TrackAtcTravelDirection.BtoA;
            }

            Assert.That(Resolve(last, departure, 2, out var next, out var route, out int index), Is.True);
            Assert.That(next, Is.SameAs(block));
            Assert.That(route, Is.Null);
            Assert.That(index, Is.EqualTo(-1));
        }

        [TestCase(TrackAtcTravelDirection.BtoA)]
        [TestCase(TrackAtcTravelDirection.Unspecified)]
        public void DoesNotExitToBlockWithWrongDirection(TrackAtcTravelDirection direction)
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = direction;
            AssertStopped(last, departure, 2);
        }

        [Test]
        public void DoesNotExitToBlockOnEntrySide()
        {
            var block = AddEdge("Block", "D", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            AssertStopped(last, departure, 2);
        }

        [Test]
        public void DoesNotChooseBlockWithoutKnownExitSide()
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            AssertUnresolved(last);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupiedOrUnknownBlockStopsAtRouteEnd(bool unknown)
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            if (unknown)
                context.Input.OccupiedByCircuitId.Remove(block.trackCircuitId);
            else
                context.Input.OccupiedByCircuitId[block.trackCircuitId] = true;
            AssertStopped(last, departure, 2);
        }

        [Test]
        public void ExitsToBlockOnSameOccupiedCircuit()
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            block.trackCircuitId = last.trackCircuitId;
            context.Input.OccupiedByCircuitId[last.trackCircuitId] = true;
            Assert.That(Resolve(last, departure, 2, out var next, out _, out _), Is.True);
            Assert.That(next, Is.SameAs(block));
        }

        [Test]
        public void RejectsAmbiguousExitBlocks()
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            var other = AddEdge("OtherBlock", "E", "G");
            other.controlKind = TrackAtcEdgeControlKind.Block;
            other.direction = TrackAtcTravelDirection.AtoB;
            Assert.That(Resolve(last, departure, 2, out var next, out var route, out int index), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(route, Is.Null);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void CalculateContinuesThroughBlockAfterRouteEnd()
        {
            var block = AddEdge("Block", "E", "F");
            block.controlKind = TrackAtcEdgeControlKind.Block;
            block.direction = TrackAtcTravelDirection.AtoB;
            var nextBlock = AddEdge("NextBlock", "F", "G");
            nextBlock.controlKind = TrackAtcEdgeControlKind.Block;
            nextBlock.direction = TrackAtcTravelDirection.AtoB;

            TrackAtcLogic.CalculateNextEdge(context, first);
            AssertTelegramPath(last, "Last", "Block", "NextBlock");
            AssertTelegramPath(block, "Block", "NextBlock");
            AssertTelegramPath(nextBlock, "NextBlock");
        }

        [Test]
        public void RejectsAmbiguousSuccessors()
        {
            AddRoute("Other", "Shared", "Middle", "Last");
            Assert.That(Resolve(shared, arrival, 1, out var next, out var route, out int index), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(route, Is.Null);
            Assert.That(index, Is.EqualTo(-1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupiedOrUnknownNextCircuitStopsBeforeSwitching(bool unknown)
        {
            if (unknown)
                context.Input.OccupiedByCircuitId.Remove(middle.trackCircuitId);
            else
                context.Input.OccupiedByCircuitId[middle.trackCircuitId] = true;
            AssertStopped(shared, arrival, 1);
        }

        [Test]
        public void SameCircuitOccupancyDoesNotStopTraversal()
        {
            middle.trackCircuitId = shared.trackCircuitId;
            context.Input.OccupiedByCircuitId[shared.trackCircuitId] = true;
            Assert.That(Resolve(shared, arrival, 1, out var next, out _, out _), Is.True);
            Assert.That(next, Is.SameAs(middle));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(2)]
        public void RejectsIndexThatDoesNotIdentifyCurrentEdge(int index)
        {
            Assert.That(Resolve(shared, arrival, index, out var next, out var route, out int nextIndex), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(route, Is.Null);
            Assert.That(nextIndex, Is.EqualTo(-1));
        }

        [Test]
        public void RejectsDisconnectedNextEdge()
        {
            last.atcNodeAId = "Other";
            Assert.That(Resolve(middle, departure, 1, out var next, out _, out _), Is.False);
            Assert.That(next, Is.Null);
        }

        [Test]
        public void CalculateCarriesRouteAndIndexAcrossSharedEdge()
        {
            TrackAtcLogic.CalculateNextEdge(context, first);
            AssertTelegramPath(first, "First", "Shared", "Middle", "Last");
            AssertTelegramPath(shared, "Shared", "Middle", "Last");
            AssertTelegramPath(middle, "Middle", "Last");
            AssertTelegramPath(last, "Last");
        }

        [Test]
        public void CalculateCarriesRouteSelectedFromBlock()
        {
            var before = AddEdge("Before", "BeforeNode", "A");
            before.controlKind = TrackAtcEdgeControlKind.Block;
            before.direction = TrackAtcTravelDirection.AtoB;

            TrackAtcLogic.CalculateNextEdge(context, before);
            AssertTelegramPath(before, "Before", "First", "Shared", "Middle", "Last");
            AssertTelegramPath(first, "First", "Shared", "Middle", "Last");
            AssertTelegramPath(shared, "Shared", "Middle", "Last");
            AssertTelegramPath(last, "Last");
        }

        private void AssertTelegramPath(TrackAtcGraphEdge edge, params string[] path)
        {
            var telegram = context.Output.telegrams[edge.trackCircuitId];
            Assert.That(telegram.isValid, Is.True);
            Assert.That(telegram.atcRouteInfomation[(edge.atcEdgeId, TrackAtcTravelDirection.AtoB)].atcEdgePath,
                Is.EqualTo(path));
        }

        private void AssertUnresolved(TrackAtcGraphEdge edge)
        {
            Assert.That(Resolve(edge, null, -1, out var next, out var route, out int index), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(route, Is.Null);
            Assert.That(index, Is.EqualTo(-1));
        }

        private bool Resolve(TrackAtcGraphEdge edge, TrackAtcRouteDefinition route, int index,
            out TrackAtcGraphEdge next, out TrackAtcRouteDefinition nextRoute, out int nextIndex) =>
            TrackAtcLogic.TryResolveNextEdgeOnInterlocking(context, edge, route, index,
                out next, out nextRoute, out nextIndex, out _);

        private void AssertStopped(TrackAtcGraphEdge edge, TrackAtcRouteDefinition route, int index)
        {
            Assert.That(Resolve(edge, route, index, out var next, out var nextRoute, out int nextIndex), Is.True);
            Assert.That(next, Is.Null);
            Assert.That(nextRoute, Is.Null);
            Assert.That(nextIndex, Is.EqualTo(-1));
        }

        private TrackAtcGraphEdge AddEdge(string id, string a, string b)
        {
            var edge = new TrackAtcGraphEdge
            {
                atcEdgeId = id, atcNodeAId = a, atcNodeBId = b, trackCircuitId = id,
                controlKind = TrackAtcEdgeControlKind.Interlocking, lengthM = 100f
            };
            context.Workspace.atcEdgesById.Add(id, edge);
            context.Input.OccupiedByCircuitId.Add(id, false);
            foreach (string nodeId in new[] { a, b })
            {
                if (!context.Workspace.atcNodesById.TryGetValue(nodeId, out var node))
                {
                    node = new TrackAtcGraphNode
                    {
                        atcNodeId = nodeId, connectedAtcEdgeIds = new List<string>()
                    };
                    context.Workspace.atcNodesById.Add(nodeId, node);
                }
                node.connectedAtcEdgeIds.Add(id);
            }
            return edge;
        }

        private TrackAtcRouteDefinition AddRoute(string id, params string[] edgeIds)
        {
            var route = new TrackAtcRouteDefinition
            {
                atcRouteId = id, interlockingRouteId = id, atcEdgeIds = new List<string>(edgeIds)
            };
            context.Workspace.atcRoutesById.Add(id, route);
            context.Input.RoutesById.Add(id, new TrackAtcRouteInput
            {
                ProceedAllowed = true, PathEstablished = true, RouteLocked = true
            });
            return route;
        }
    }
}
