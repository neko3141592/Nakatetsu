using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcOrpHoldTests
    {
        [TestCase(OverrunProtectionMode.None)]
        [TestCase(OverrunProtectionMode.Normal)]
        public void SameTerminalRetainsRestrictedAndDoesNotRewriteTelegram(OverrunProtectionMode receivedMode)
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Receive(context, receivedMode, TrackAtcTravelDirection.AtoB, "a", "b", "c");

            AssertRestricted(context);
            Assert.That(context.Input.frontTelegram.atcRouteInfomation[("a", TrackAtcTravelDirection.AtoB)]
                .overrunProtectionMode, Is.EqualTo(receivedMode));
            TrainAtcLogic.Calculate(context);
            AssertRestricted(context);
        }

        [Test]
        public void NormalAndInitiallyNoneDoNotStartOrpHold()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Normal, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertNone(context);

            context = CreateContext();
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertNone(context);
        }

        [Test]
        public void NewTerminalAdoptsReceivedMode()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b");
            AssertNone(context);
            Assert.That(context.State.pattern.atcEdgePath, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void OppositeDirectionOnSameTerminalAdoptsReceivedMode()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Correct(context, "c", false);
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.BtoA, "c");
            AssertNone(context);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangingCurrentEdgeDirectionKeepsHoldForSameTerminalDirection(bool reverse)
        {
            var context = CreateContext(true, reverse ? "c" : "a", !reverse);
            var firstDirection = reverse ? TrackAtcTravelDirection.BtoA : TrackAtcTravelDirection.AtoB;
            var firstPath = reverse ? new[] { "c", "b", "a" } : new[] { "a", "b", "c" };
            Receive(context, OverrunProtectionMode.Restricted, firstDirection, firstPath);

            // 中間EdgeのA/Bは逆向き。現在Edgeの方向が変わっても終端の方向は変わらない。
            Correct(context, "b", reverse);
            var middleDirection = reverse ? TrackAtcTravelDirection.AtoB : TrackAtcTravelDirection.BtoA;
            var shortenedPath = reverse ? new[] { "b", "a" } : new[] { "b", "c" };
            Receive(context, OverrunProtectionMode.None, middleDirection, shortenedPath);
            AssertRestricted(context);
            Assert.That(context.State.pattern.atcEdgePath, Is.EqualTo(shortenedPath));

            string terminal = reverse ? "a" : "c";
            Correct(context, terminal, !reverse);
            Receive(context, OverrunProtectionMode.None, firstDirection, terminal);
            AssertRestricted(context);
            Assert.That(context.State.pattern.atcEdgePath, Is.EqualTo(new[] { terminal }));
        }

        [Test]
        public void OpeningReleasesOldHoldAndLaterRestrictedCanHoldSameTargetAgain()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Input.doorOpeningOperationRevision++;
            context.Input.areAllDoorsClosed = false;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertNone(context);

            // 開扉状態が続くだけでは毎tick解除しない。同じ着点でも新たに保持できる。
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertRestricted(context);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpeningDuringInactiveOrInvalidTickIsNotLost(bool invalidInput)
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            if (invalidInput)
            {
                context.Input.hasCarMasses = false;
            }
            else
            {
                context.Input.cab.isKeyInserted = false;
            }
            context.Input.doorOpeningOperationRevision++;
            // 開扉後に閉扉してから復帰しても、受け付けた操作番号で解除を確認する。
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Input.hasCarMasses = true;
            context.Input.cab.isKeyInserted = true;
            TrainAtcLogic.Calculate(context);
            AssertNone(context);
        }

        [Test]
        public void MissingOpeningSourceAndNonclosedDoorsDoNotReleaseHold()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Input.hasDoorOpeningOperation = false;
            context.Input.doorOpeningOperationRevision++;
            context.Input.hasDoorState = false;
            context.Input.areAllDoorsClosed = false;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertRestricted(context);

            context.Input.hasDoorOpeningOperation = true;
            TrainAtcLogic.Calculate(context);
            AssertNone(context);
        }

        [Test]
        public void HoldSurvivesSignalGraceAndInactiveTickUntilSameTargetIsRead()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.5f;
            TrainAtcLogic.Calculate(context);
            AssertRestricted(context);
            context.Input.cab.isKeyInserted = false;
            TrainAtcLogic.Calculate(context);
            context.Input.cab.isKeyInserted = true;
            context.Input.deltaTimeSeconds = 0f;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertRestricted(context);
        }

        [Test]
        public void OpeningDuringSignalGraceClearsHoldBeforeReceptionReturns()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.5f;
            context.Input.doorOpeningOperationRevision++;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.pattern.isValid, Is.True);

            context.Input.deltaTimeSeconds = 0f;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertNone(context);
        }

        [TestCase("mode")]
        [TestCase("terminal")]
        [TestCase("connection")]
        public void HoldDoesNotMakeInvalidReceivedRouteUsable(string invalidPart)
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            var telegram = Telegram(OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            var route = telegram.atcRouteInfomation[("a", TrackAtcTravelDirection.AtoB)];
            switch (invalidPart)
            {
                case "mode": route.overrunProtectionMode = (OverrunProtectionMode)99; break;
                case "terminal": route.stopAtcEdgeId = "b"; break;
                case "connection": route.atcEdgePath = new List<string> { "a", "c" }; break;
            }
            context.Input.frontTelegram = telegram;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isAtcHealthy, Is.False);
            Assert.That(context.State.pattern.isValid, Is.False);
            Assert.That(context.Output.brake.isEmergencyBrakeRequired, Is.True);

            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertRestricted(context);
        }

        [Test]
        public void HeldRestrictedValidatesOrpSettingsInsteadOfUnusedNoneSettings()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Settings.serviceStopMarginM = float.NaN;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            AssertRestricted(context);

            context.Settings.serviceStopMarginM = 100f;
            context.Settings.orpDecelerationMps2 = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.validation.isInputValid, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
            Assert.That(context.State.pattern.isValid, Is.False);
        }

        [Test]
        public void OpeningValidatesTheNewNoneSettings()
        {
            var context = CreateContext();
            Receive(context, OverrunProtectionMode.Restricted, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            context.Settings.serviceStopMarginM = float.NaN;
            context.Input.doorOpeningOperationRevision++;
            Receive(context, OverrunProtectionMode.None, TrackAtcTravelDirection.AtoB, "a", "b", "c");
            Assert.That(context.State.validation.isInputValid, Is.False);
            Assert.That(context.State.isAtcHealthy, Is.False);
        }

        private static TrainAtcContext CreateContext(
            bool reverseMiddleEdge = false, string currentEdge = "a", bool frontFacesAtoB = true)
        {
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 72f };
            graph.atcEdge.Add(Edge("a", "n0", "n1"));
            graph.atcEdge.Add(reverseMiddleEdge ? Edge("b", "n2", "n1") : Edge("b", "n1", "n2"));
            graph.atcEdge.Add(Edge("c", "n2", "n3"));
            var context = new TrainAtcContext();
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph,
                Position(currentEdge, frontFacesAtoB), Position(currentEdge, frontFacesAtoB)), Is.True);
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
            context.Input.frontReceiverDistanceFromFrontM = 10f;
            context.Input.rearReceiverDistanceFromFrontM = 10f;
            context.Input.hasBrakeSettings = true;
            context.Input.brakeSettings.brakeSubstepCount = 1;
            context.Input.brakeSettings.maximumServiceBrakeStep = 1;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.Add(0.5f);
            context.Input.hasDoorState = true;
            context.Input.areAllDoorsClosed = true;
            context.Input.hasDoorOpeningOperation = true;
            return context;
        }

        private static TrackAtcGraphEdge Edge(string id, string nodeA, string nodeB)
        {
            var edge = new TrackAtcGraphEdge
            {
                atcEdgeId = id,
                atcNodeAId = nodeA,
                atcNodeBId = nodeB,
                trackCircuitId = id,
                lengthM = 300f
            };
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = edge.lengthM });
            return edge;
        }

        private static TrainAtcPosition Position(string edgeId, bool frontFacesAtoB)
        {
            return new TrainAtcPosition
            {
                atcEdgeId = edgeId,
                distanceOnAtcEdgeM = 50f,
                frontFacesAtoB = frontFacesAtoB
            };
        }

        private static void Correct(TrainAtcContext context, string edgeId, bool frontFacesAtoB)
        {
            Assert.That(TrainAtcLogic.TryCorrectPosition(context,
                Position(edgeId, frontFacesAtoB), Position(edgeId, frontFacesAtoB)), Is.True);
        }

        private static void Receive(
            TrainAtcContext context, OverrunProtectionMode mode, TrackAtcTravelDirection direction,
            params string[] path)
        {
            context.Input.frontTelegram = Telegram(mode, direction, path);
            TrainAtcLogic.Calculate(context);
        }

        private static TrackCircuitAtcTelegram Telegram(
            OverrunProtectionMode mode, TrackAtcTravelDirection direction, params string[] path)
        {
            var telegram = new TrackCircuitAtcTelegram { isValid = true };
            telegram.atcRouteInfomation.Add((path[0], direction), new TrackCircuitAtcRouteInfomation
            {
                atcEdgePath = new List<string>(path),
                stopAtcEdgeId = path[path.Length - 1],
                overrunProtectionMode = mode
            });
            return telegram;
        }

        private static void AssertRestricted(TrainAtcContext context)
        {
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.State.protectionMode.isProtectionModeKnown, Is.True);
            Assert.That(context.State.protectionMode.overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.Restricted));
            Assert.That(context.State.pattern.isValid, Is.True);
            Assert.That(context.State.pattern.orpPattern.samples.Count, Is.GreaterThan(1));
        }

        private static void AssertNone(TrainAtcContext context)
        {
            Assert.That(context.State.isAtcHealthy, Is.True);
            Assert.That(context.State.protectionMode.isProtectionModeKnown, Is.True);
            Assert.That(context.State.protectionMode.overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.None));
            Assert.That(context.State.pattern.isValid, Is.True);
            Assert.That(context.State.pattern.orpPattern.samples, Is.Empty);
        }
    }
}
