using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using NUnit.Framework;
using EdgeDirection = Nakatetsu.Track.Graph.Edge.TrackEdgeTravelDirection;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackAtcProcessingTests
    {
        private TrackAtcContext context;
        private TrackAtcGraphDefinition graph;
        private TrackAtcGraphEdge first;
        private TrackAtcGraphEdge shared;
        private TrackAtcGraphEdge middle;
        private TrackAtcGraphEdge last;
        private TrackAtcRouteDefinition arrival;
        private TrackAtcRouteDefinition departure;

        [SetUp]
        public void SetUp()
        {
            CreateContext();
            first = AddEdge("First", "A", "B");
            shared = AddEdge("Shared", "B", "C");
            middle = AddEdge("Middle", "C", "D");
            last = AddEdge("Last", "D", "E");
            arrival = AddRoute("ArrivalAtc", "ArrivalInterlocking", "First", "Shared");
            departure = AddRoute("DepartureAtc", "DepartureInterlocking", "Shared", "Middle", "Last");
        }

        [Test]
        public void KnownUnsetRoutesSendSingleEdgeNoneInBothDirections()
        {
            TrackAtcLogic.Calculate(context);

            // 静的進路定義は片方向だけでも、未設定の停止電文は両方向へ送る。
            Assert.That(context.Output.telegrams.Count, Is.EqualTo(4));
            foreach (var edge in graph.atcEdge)
            {
                AssertRoute(edge, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, edge.atcEdgeId);
                AssertRoute(edge, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.None, edge.atcEdgeId);
                Assert.That(context.Output.telegrams[edge.trackCircuitId].issuedAtSeconds, Is.EqualTo(120d));
            }
        }

        [Test]
        public void DepartureSettingExtendsPathAndUsesItsInterlockingMode()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");

            SetRoute(departure, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                "First", "Shared", "Middle", "Last");
            Assert.That(Result("First").terminalAtcRouteId, Is.EqualTo(departure.atcRouteId));
            Assert.That(Result("First").nextEdgeKey, Is.EqualTo(Key("Shared")));
            Assert.That(Result("First").affectedCircuitIds, Is.Empty);
        }

        [Test]
        public void RouteOriginAndInteriorContinueAfterEntryPermissionDrops()
        {
            SetRoute(departure, OverrunProtectionMode.Restricted).ProceedAllowed = false;
            TrackAtcLogic.Calculate(context);

            AssertRoute(shared, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted,
                "Shared", "Middle", "Last");
            AssertRoute(middle, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "Middle", "Last");
        }

        [Test]
        public void SuccessorWithoutEntryPermissionDoesNotExtendUpstreamStopLimit()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal).ProceedAllowed = false;
            TrackAtcLogic.Calculate(context);

            // Shared自身の起点は継続できても、上流起点の停止限界はSharedのまま。
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
            AssertRoute(shared, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                "Shared", "Middle", "Last");
            Assert.That(Result("First").stopAtcEdgeId, Is.EqualTo("Shared"));
            Assert.That(Result("Shared").nextEdgeKey, Is.EqualTo(Key("Middle")));
        }

        [Test]
        public void EntryFromBlockRequiresProceedPermission()
        {
            var before = AddEdge("Before", "BeforeNode", "A", TrackAtcEdgeControlKind.Block);
            SetRoute(arrival, OverrunProtectionMode.Restricted).ProceedAllowed = false;
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "Before");
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");

            context.Input.RoutesById[arrival.interlockingRouteId].ProceedAllowed = true;
            TrackAtcLogic.Calculate(context);
            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted,
                "Before", "First", "Shared");
        }

        [Test]
        public void PermittedEntryIntoUnlockedRouteStopsAtItsFirstEdge()
        {
            var before = AddEdge("Before", "BeforeNode", "A", TrackAtcEdgeControlKind.Block);
            SetRoute(arrival, OverrunProtectionMode.Restricted).RouteLocked = false;
            TrackAtcLogic.Calculate(context);

            // 新規進入でFirstまでは入れるが、進路内の継続には鎖錠が必要。
            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "Before", "First");
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "First");
            Assert.That(Result("Before").nextEdgeKey, Is.EqualTo(Key("First")));
            Assert.That(Result("First").nextEdgeKey, Is.Null);
        }

        [Test]
        public void UnlockedDepartureCannotExtendBeyondSharedArrivalTerminal()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal).RouteLocked = false;
            TrackAtcLogic.Calculate(context);

            // 共有Edgeで発車進路に入場できても、その先へ継続できなければ到着進路の終端に止まる。
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
            AssertRoute(shared, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "Shared");
            Assert.That(Result("First").terminalAtcRouteId, Is.EqualTo(arrival.atcRouteId));
            Assert.That(Result("Shared").nextEdgeKey, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RouteCanExitThroughBlockWithLocallyReversedEndpoints(bool reversedEndpoints)
        {
            var block = reversedEndpoints
                ? AddEdge("Block", "F", "E", TrackAtcEdgeControlKind.Block)
                : AddEdge("Block", "E", "F", TrackAtcEdgeControlKind.Block);
            block.direction = reversedEndpoints ? TrackAtcTravelDirection.BtoA : TrackAtcTravelDirection.AtoB;
            AddEdge("NextBlock", "F", "G", TrackAtcEdgeControlKind.Block);
            SetRoute(arrival, OverrunProtectionMode.Normal);
            SetRoute(departure, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None,
                "First", "Shared", "Middle", "Last", "Block", "NextBlock");
            AssertRoute(block, block.direction, OverrunProtectionMode.None, "Block", "NextBlock");
            Assert.That(Result("Last").nextEdgeKey, Is.EqualTo(Key("Block",
                reversedEndpoints ? EdgeDirection.BtoA : EdgeDirection.AtoB)));
            Assert.That(Result("First").terminalAtcRouteId, Is.Null);
        }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void UnusableOriginRouteProducesValidSingleEdgeNone(bool established, bool locked, bool cancelled)
        {
            var routeInput = SetRoute(departure, OverrunProtectionMode.Restricted);
            routeInput.PathEstablished = established;
            routeInput.RouteLocked = locked;
            routeInput.CancelPending = cancelled;
            TrackAtcLogic.Calculate(context);

            AssertRoute(middle, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "Middle");
        }

        [Test]
        public void CancelledArrivalDoesNotHideUsableDepartureOnSharedEdge()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted).CancelPending = true;
            SetRoute(departure, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "First");
            AssertRoute(shared, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                "Shared", "Middle", "Last");
        }

        [Test]
        public void OccupiedOriginCircuitDoesNotPreventExploration()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            context.Input.OccupiedByCircuitId[first.trackCircuitId] = true;
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
        }

        [Test]
        public void OccupiedCompositeCircuitKeepsItsSelectedPath()
        {
            CreateTurnoutContext(out var before, out _, out var reverse, out _, out var reverseRoute);
            SetRoute(reverseRoute, OverrunProtectionMode.Restricted);
            context.Input.OccupiedByCircuitId["21T"] = true;
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T");
            AssertRoute(reverse, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "21T_R");
        }

        [Test]
        public void OccupiedNextCircuitStopsBeforeItsEdge()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal);
            context.Input.OccupiedByCircuitId[middle.trackCircuitId] = true;
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
            Assert.That(Result("First").endReason, Is.EqualTo(TrackAtcPathEndReason.NextCircuitOccupied));
            AssertRoute(middle, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal, "Middle", "Last");
        }

        [Test]
        public void MissingNextCircuitInputFailsInsteadOfReportingKnownStop()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal);
            context.Input.OccupiedByCircuitId.Remove(middle.trackCircuitId);
            TrackAtcLogic.Calculate(context);

            AssertInvalid(first.trackCircuitId);
            Assert.That(Result("First").isPathValid, Is.False);
            Assert.That(Result("First").endReason, Is.EqualTo(TrackAtcPathEndReason.CalculationFailed));
            Assert.That(Result("First").failureReason, Is.Not.Empty);
        }

        [Test]
        public void MissingRouteInputFailsInsteadOfUsingNoneFallback()
        {
            context.Input.RoutesById.Remove(arrival.interlockingRouteId);
            TrackAtcLogic.Calculate(context);

            AssertInvalid(first.trackCircuitId);
            Assert.That(Result("First").isPathValid, Is.False);
            Assert.That(context.State.validation.hasRouteInputById[arrival.interlockingRouteId], Is.False);
        }

        [Test]
        public void InvalidTerminalModeFailsWithoutChangingSuccessfulPath()
        {
            SetRoute(arrival, (OverrunProtectionMode)999);
            TrackAtcLogic.Calculate(context);

            Assert.That(Result("First").isPathValid, Is.True);
            Assert.That(context.State.protectionMode.resultsByKey[Key("First")].isProtectionModeKnown, Is.False);
            AssertInvalid(first.trackCircuitId);
            AssertInvalid(shared.trackCircuitId);
            AssertRoute(middle, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "Middle");
        }

        [Test]
        public void UnselectedBranchDoesNotInvalidateSelectedCircuit()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            var inactive = AddEdge("InactiveYard", "A", "Other", TrackAtcEdgeControlKind.Yard);
            inactive.trackCircuitId = first.trackCircuitId;
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key("InactiveYard")), Is.False);
        }

        [Test]
        public void AmbiguousUsableRoutesFailInsteadOfChoosingEnumerationOrder()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal);
            var other = AddRoute("OtherDepartureAtc", "OtherDepartureInterlocking", "Shared", "Middle", "Last");
            SetRoute(other, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);

            AssertInvalid(first.trackCircuitId);
            AssertInvalid(shared.trackCircuitId);
            Assert.That(Result("Shared").isPathValid, Is.False);
        }

        [Test]
        public void CyclicBlockPathProducesInvalidTelegrams()
        {
            CreateContext();
            AddEdge("Cycle1", "A", "B", TrackAtcEdgeControlKind.Block);
            AddEdge("Cycle2", "B", "C", TrackAtcEdgeControlKind.Block);
            AddEdge("Cycle3", "C", "A", TrackAtcEdgeControlKind.Block);
            TrackAtcLogic.Calculate(context);

            Assert.That(context.Output.telegrams.Count, Is.EqualTo(3));
            foreach (var edge in graph.atcEdge)
            {
                AssertInvalid(edge.trackCircuitId);
                Assert.That(Result(edge.atcEdgeId).isPathValid, Is.False);
            }
        }

        [Test]
        public void MixedEndpointDirectionsRemainLocalToEachEdge()
        {
            // Node接続は変えず、各EdgeのA/B表記だけ反転する。
            (shared.atcNodeAId, shared.atcNodeBId) = (shared.atcNodeBId, shared.atcNodeAId);
            (last.atcNodeAId, last.atcNodeBId) = (last.atcNodeBId, last.atcNodeAId);
            departure.entryDirection = TrackAtcTravelDirection.BtoA;
            SetRoute(arrival, OverrunProtectionMode.Normal);
            SetRoute(departure, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted,
                "First", "Shared", "Middle", "Last");
            AssertRoute(shared, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.Restricted,
                "Shared", "Middle", "Last");
            AssertRoute(last, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.Restricted, "Last");
            Assert.That(Result("First").nextEdgeKey, Is.EqualTo(Key("Shared", EdgeDirection.BtoA)));
            Assert.That(Result("First").stopTravelDirection, Is.EqualTo(EdgeDirection.BtoA));
        }

        [Test]
        public void OppositeRoutesHaveIndependentDirectionsAndStopLimits()
        {
            SetRoute(arrival, OverrunProtectionMode.Normal);
            SetRoute(departure, OverrunProtectionMode.Restricted);
            var reverse = AddRoute("ReverseAtc", "ReverseInterlocking", "Last", "Middle", "Shared");
            SetRoute(reverse, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);

            AssertRoute(shared, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted,
                "Shared", "Middle", "Last");
            AssertRoute(last, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.Normal, "Last", "Middle", "Shared");
            Assert.That(Result("Last", EdgeDirection.BtoA).stopAtcEdgeId, Is.EqualTo("Shared"));
            Assert.That(Result("Last", EdgeDirection.BtoA).stopTravelDirection, Is.EqualTo(EdgeDirection.BtoA));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidClockCannotProduceValidTelegrams(double time)
        {
            context.Input.simulationTimeSeconds = time;
            TrackAtcLogic.Calculate(context);

            Assert.That(context.State.validation.isSimulationTimeValid, Is.False);
            Assert.That(context.Output.telegrams.Count, Is.EqualTo(4));
            foreach (var edge in graph.atcEdge)
            {
                AssertInvalid(edge.trackCircuitId);
            }
        }

        [Test]
        public void BrokenGraphClearsPreviousOutputAndLookupState()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);
            Assert.That(context.Output.telegrams, Is.Not.Empty);

            graph.atcNode.Clear();
            TrackAtcLogic.Calculate(context);

            Assert.That(context.State.validation.isGraphValid, Is.False);
            Assert.That(context.State.validation.atcEdgesById, Is.Empty);
            Assert.That(context.State.path.resultsByKey, Is.Empty);
            Assert.That(context.Output.telegrams, Is.Empty);
        }

        [Test]
        public void NullGraphClearsPreviousOutput()
        {
            TrackAtcLogic.Calculate(context);
            context.Graph = null;
            TrackAtcLogic.Calculate(context);

            Assert.That(context.Output.telegrams, Is.Empty);
            Assert.That(context.State.path.resultsByKey, Is.Empty);
        }

        [Test]
        public void NextTickShortensPathWithoutRetainingOldDeparture()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                "First", "Shared", "Middle", "Last");

            context.Input.RoutesById[departure.interlockingRouteId] = new TrackAtcRouteInput();
            context.Input.simulationTimeSeconds = 121d;
            TrackAtcLogic.Calculate(context);

            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "First", "Shared");
            AssertRoute(middle, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "Middle");
            Assert.That(context.Output.telegrams[first.trackCircuitId].issuedAtSeconds, Is.EqualTo(121d));
            Assert.That(Result("Shared").nextEdgeKey, Is.Null);
        }

        [Test]
        public void GraphReplacementRemovesPreviousCircuitsAndResults()
        {
            TrackAtcLogic.Calculate(context);
            var replacement = new TrackAtcGraphDefinition();
            replacement.atcEdge.Add(new TrackAtcGraphEdge
            {
                atcEdgeId = "Replacement", trackCircuitId = "ReplacementCircuit",
                atcNodeAId = "R1", atcNodeBId = "R2", lengthM = 100f,
                controlKind = TrackAtcEdgeControlKind.Block, direction = TrackAtcTravelDirection.AtoB
            });
            replacement.atcNode.Add(new TrackAtcGraphNode
            {
                atcNodeId = "R1", connectedAtcEdgeIds = new List<string> { "Replacement" }
            });
            replacement.atcNode.Add(new TrackAtcGraphNode
            {
                atcNodeId = "R2", connectedAtcEdgeIds = new List<string> { "Replacement" }
            });
            context.Graph = replacement;
            context.Input.OccupiedByCircuitId["ReplacementCircuit"] = false;
            TrackAtcLogic.Calculate(context);

            Assert.That(context.Output.telegrams.Keys, Is.EquivalentTo(new[] { "ReplacementCircuit" }));
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key("First")), Is.False);
        }

        [Test]
        public void OutputPathsDoNotShareWritableListsWithOtherRoutesOrPreviousTick()
        {
            SetRoute(arrival, OverrunProtectionMode.Restricted);
            SetRoute(departure, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);
            var oldPath = GetRoute(first, TrackAtcTravelDirection.AtoB).atcEdgePath;
            var sharedPath = GetRoute(shared, TrackAtcTravelDirection.AtoB).atcEdgePath;
            oldPath.Clear();

            Assert.That(sharedPath, Is.EqualTo(new[] { "Shared", "Middle", "Last" }));
            Assert.That(Result("First").nextEdgeKey, Is.EqualTo(Key("Shared")));

            TrackAtcLogic.Calculate(context);
            AssertRoute(first, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                "First", "Shared", "Middle", "Last");
            Assert.That(GetRoute(first, TrackAtcTravelDirection.AtoB).atcEdgePath, Is.Not.SameAs(oldPath));
            Assert.That(oldPath, Is.Empty);
        }

        [Test]
        public void UnsetTurnoutStopsBeforeEntryButAllowsCurrentEdgeToItsExit()
        {
            CreateTurnoutContext(out var before, out var normal, out _, out _, out _);
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T");
            AssertRoute(normal, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "21T_N");
            AssertRoute(normal, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.None, "21T_N");
        }

        [Test]
        public void EstablishedReverseRouteSendsOnlyReverseEdgeInBothDirectionFields()
        {
            CreateTurnoutContext(out var before, out var normal, out var reverse, out _, out var reverseRoute);
            // 開通済み連動結果を採用し、初期選択用の線路接続を再照査しない。
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            context.Input.PhysicalPathAvailableByAtcEdgeId[reverse.atcEdgeId] = false;
            SetRoute(reverseRoute, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "20T", "21T_R");
            AssertRoute(reverse, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Restricted, "21T_R");
            AssertRoute(reverse, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.None, "21T_R");
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key(normal.atcEdgeId)), Is.False);
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key(normal.atcEdgeId, EdgeDirection.BtoA)), Is.False);
        }

        [Test]
        public void ReleasedTurnoutRouteKeepsEnteredEdgeUntilCircuitClears()
        {
            CreateTurnoutContext(out var before, out var normal, out var reverse, out _, out var reverseRoute);
            SetRoute(reverseRoute, OverrunProtectionMode.Restricted);
            context.Input.OccupiedByCircuitId["21T"] = true;
            TrackAtcLogic.Calculate(context);

            context.Input.RoutesById[reverseRoute.interlockingRouteId] = new TrackAtcRouteInput();
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            context.Input.PhysicalPathAvailableByAtcEdgeId[reverse.atcEdgeId] = false;
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T");
            AssertRoute(reverse, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "21T_R");
            AssertRoute(reverse, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.None, "21T_R");

            context.Input.OccupiedByCircuitId["21T"] = false;
            TrackAtcLogic.Calculate(context);
            AssertRoute(normal, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "21T_N");
        }

        [Test]
        public void EstablishedRouteCanReplaceInitialCommonSpanSelection()
        {
            CreateTurnoutContext(out _, out var normal, out var reverse, out _, out var reverseRoute);
            context.Input.OccupiedByCircuitId["21T"] = true;
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            TrackAtcLogic.Calculate(context);
            AssertRoute(normal, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "21T_N");

            SetRoute(reverseRoute, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);
            AssertRoute(reverse, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal, "21T_R");
        }

        [Test]
        public void MissingTurnoutRouteInputCannotBecomeValidStopTelegram()
        {
            CreateTurnoutContext(out var before, out var normal, out _, out _, out var reverseRoute);
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            context.Input.RoutesById.Remove(reverseRoute.interlockingRouteId);
            TrackAtcLogic.Calculate(context);

            AssertInvalid("21T");
            AssertInvalid(before.trackCircuitId);
        }

        [Test]
        public void MissingInitialConnectionDoesNotGuessTurnoutBranch()
        {
            CreateTurnoutContext(out var before, out _, out _, out _, out _);
            TrackAtcLogic.Calculate(context);

            AssertInvalid("21T");
            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T");
        }

        [Test]
        public void MultipleEstablishedTurnoutBranchesAreInvalid()
        {
            CreateTurnoutContext(out _, out _, out _, out var normalRoute, out var reverseRoute);
            SetRoute(normalRoute, OverrunProtectionMode.Normal);
            SetRoute(reverseRoute, OverrunProtectionMode.Restricted);
            TrackAtcLogic.Calculate(context);

            AssertInvalid("21T");
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key("21T_N")), Is.False);
            Assert.That(context.State.path.resultsByKey.ContainsKey(Key("21T_R")), Is.False);
        }

        [Test]
        public void SingleEdgeRouteUsesItsExplicitEntryDirection()
        {
            CreateContext();
            var edge = AddEdge("One", "A", "B");
            var route = AddRoute("OneAtc", "OneInterlocking", "One");
            route.entryDirection = TrackAtcTravelDirection.BtoA;
            SetRoute(route, OverrunProtectionMode.Normal);
            TrackAtcLogic.Calculate(context);

            AssertRoute(edge, TrackAtcTravelDirection.BtoA, OverrunProtectionMode.Normal, "One");
            AssertRoute(edge, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "One");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RouteBeginningWithBlockChecksEntryPermissionBeforeTurnout(bool proceedAllowed)
        {
            CreateTurnoutContext(out var before, out var normal, out _, out _, out _);
            var beyond = AddEdge("2RT", "NormalExit", "Beyond");
            var route = AddRoute("20T-21T-2RT", "ThroughInterlocking", "20T", "21T_N", "2RT");
            SetRoute(route, OverrunProtectionMode.Normal).ProceedAllowed = proceedAllowed;
            TrackAtcLogic.Calculate(context);

            if (proceedAllowed)
            {
                AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal,
                    "20T", "21T_N", "2RT");
            }
            else
            {
                AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T");
            }

            // 21T内の列車は進入許可が落ちても、開通・鎖錠中の進路内を継続できる。
            AssertRoute(normal, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal, "21T_N", "2RT");
            AssertRoute(beyond, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.Normal, "2RT");
        }

        [Test]
        public void RouteBeginningWithBlockStillNeedsLockForInteriorContinuation()
        {
            CreateTurnoutContext(out var before, out var normal, out _, out _, out _);
            AddEdge("2RT", "NormalExit", "Beyond");
            context.Input.PhysicalPathAvailableByAtcEdgeId[normal.atcEdgeId] = true;
            var route = AddRoute("20T-21T-2RT", "ThroughInterlocking", "20T", "21T_N", "2RT");
            SetRoute(route, OverrunProtectionMode.Normal).RouteLocked = false;
            TrackAtcLogic.Calculate(context);

            AssertRoute(before, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "20T", "21T_N");
            AssertRoute(normal, TrackAtcTravelDirection.AtoB, OverrunProtectionMode.None, "21T_N");
        }

        private void CreateTurnoutContext(out TrackAtcGraphEdge before,
            out TrackAtcGraphEdge normal, out TrackAtcGraphEdge reverse,
            out TrackAtcRouteDefinition normalRoute, out TrackAtcRouteDefinition reverseRoute)
        {
            CreateContext();
            before = AddEdge("20T", "Start", "Common", TrackAtcEdgeControlKind.Block);
            normal = AddEdge("21T_N", "Common", "NormalExit");
            reverse = AddEdge("21T_R", "Common", "ReverseExit");
            normal.trackCircuitId = "21T";
            reverse.trackCircuitId = "21T";
            context.Input.OccupiedByCircuitId.Remove("21T_N");
            context.Input.OccupiedByCircuitId.Remove("21T_R");
            context.Input.OccupiedByCircuitId["21T"] = false;
            normalRoute = AddRoute("NormalAtc", "NormalInterlocking", "21T_N");
            reverseRoute = AddRoute("ReverseAtc", "ReverseInterlocking", "21T_R");
        }

        private void CreateContext()
        {
            graph = new TrackAtcGraphDefinition { atcGraphId = "TestGraph", maximumOperatingSpeedKmh = 120f };
            context = new TrackAtcContext { Graph = graph };
            context.Input.simulationTimeSeconds = 120d;
        }

        private TrackAtcGraphEdge AddEdge(string id, string a, string b,
            TrackAtcEdgeControlKind controlKind = TrackAtcEdgeControlKind.Interlocking)
        {
            var edge = new TrackAtcGraphEdge
            {
                atcEdgeId = id, trackCircuitId = id, atcNodeAId = a, atcNodeBId = b,
                controlKind = controlKind, direction = TrackAtcTravelDirection.AtoB, lengthM = 100f
            };
            graph.atcEdge.Add(edge);
            context.Input.OccupiedByCircuitId[id] = false;

            foreach (string nodeId in new[] { a, b })
            {
                var node = graph.atcNode.Find(candidate => candidate.atcNodeId == nodeId);
                if (node == null)
                {
                    node = new TrackAtcGraphNode { atcNodeId = nodeId };
                    graph.atcNode.Add(node);
                }
                node.connectedAtcEdgeIds.Add(id);
            }

            return edge;
        }

        private TrackAtcRouteDefinition AddRoute(string atcId, string interlockingId, params string[] edges)
        {
            var route = new TrackAtcRouteDefinition
            {
                atcRouteId = atcId, interlockingRouteId = interlockingId,
                entryDirection = TrackAtcTravelDirection.AtoB, atcEdgeIds = new List<string>(edges)
            };
            if (edges.Length > 1)
            {
                var firstEdge = graph.atcEdge.Find(edge => edge.atcEdgeId == edges[0]);
                var nextEdge = graph.atcEdge.Find(edge => edge.atcEdgeId == edges[1]);
                if (firstEdge.atcNodeAId == nextEdge.atcNodeAId || firstEdge.atcNodeAId == nextEdge.atcNodeBId)
                {
                    route.entryDirection = TrackAtcTravelDirection.BtoA;
                }
            }
            graph.routes.Add(route);
            context.Input.RoutesById[interlockingId] = new TrackAtcRouteInput();
            return route;
        }

        private TrackAtcRouteInput SetRoute(TrackAtcRouteDefinition route, OverrunProtectionMode mode)
        {
            var routeInput = new TrackAtcRouteInput
            {
                IsRouteSet = true, ProceedAllowed = true, PathEstablished = true, RouteLocked = true,
                OverrunMode = mode
            };
            context.Input.RoutesById[route.interlockingRouteId] = routeInput;
            return routeInput;
        }

        private static TrackAtcEdgeKey Key(string id, EdgeDirection direction = EdgeDirection.AtoB)
        {
            return new TrackAtcEdgeKey(id, direction);
        }

        private TrackAtcPathResult Result(string id, EdgeDirection direction = EdgeDirection.AtoB)
        {
            return context.State.path.resultsByKey[Key(id, direction)];
        }

        private TrackCircuitAtcRouteInfomation GetRoute(TrackAtcGraphEdge edge, TrackAtcTravelDirection direction)
        {
            return context.Output.telegrams[edge.trackCircuitId].GetRoute(direction);
        }

        private void AssertRoute(TrackAtcGraphEdge edge, TrackAtcTravelDirection direction,
            OverrunProtectionMode mode, params string[] path)
        {
            var telegram = context.Output.telegrams[edge.trackCircuitId];
            Assert.That(telegram.isValid, Is.True, $"{edge.atcEdgeId}/{direction}");
            var route = GetRoute(edge, direction);
            Assert.That(route, Is.Not.Null, $"{edge.atcEdgeId}/{direction}");
            Assert.That(route.atcEdgePath, Is.EqualTo(path), $"{edge.atcEdgeId}/{direction}");
            Assert.That(route.stopAtcEdgeId, Is.EqualTo(path[path.Length - 1]));
            Assert.That(route.overrunProtectionMode, Is.EqualTo(mode));
        }

        private void AssertInvalid(string circuitId)
        {
            Assert.That(context.Output.telegrams.ContainsKey(circuitId), Is.True, circuitId);
            var telegram = context.Output.telegrams[circuitId];
            Assert.That(telegram.isValid, Is.False, circuitId);
            Assert.That(telegram.routeAtoB, Is.Null, circuitId);
            Assert.That(telegram.routeBtoA, Is.Null, circuitId);
        }
    }
}
