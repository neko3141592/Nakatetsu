using System.Linq;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcLogicTests
    {
        [TestCase(OverrunProtectionMode.Restricted)]
        [TestCase(OverrunProtectionMode.Normal)]
        [TestCase(OverrunProtectionMode.None)]
        public void AppliesStopTargetsAndOrpCapsForEachMode(OverrunProtectionMode mode)
        {
            var context = CreateProtectionContext(mode, out _);
            TrainAtcLogic.Calculate(context);
            var pattern = context.State.brakePattern;
            Assert.That(pattern.normalPattern.Count, Is.EqualTo(41));
            for (int i = 0; i < 41; i++)
            {
                float normalKmh = 72f;
                float emergencyKmh = 82f;
                if (mode == OverrunProtectionMode.Restricted && i >= 20)
                {
                    normalKmh = 25f;
                    emergencyKmh = 35f;
                }
                if (mode == OverrunProtectionMode.None && i >= 20) normalKmh = 0f;
                if (mode == OverrunProtectionMode.Normal && i >= 39) normalKmh = 0f;
                if (i >= 39) emergencyKmh = 0f;
                Assert.That(pattern.normalPattern[i].speedLimitMps, Is.EqualTo(normalKmh / 3.6f).Within(0.0001f));
                Assert.That(pattern.emergencyPattern[i].speedLimitMps, Is.EqualTo(emergencyKmh / 3.6f).Within(0.0001f));
            }
        }

        [TestCase(OverrunProtectionMode.Restricted, 30f, 34, 18f, 28f)]
        [TestCase(OverrunProtectionMode.None, 40f, 32, 0f, 82f)]
        public void ReadsProtectionDistanceAndSpeedFromSettings(
            OverrunProtectionMode mode, float margin, int targetIndex, float normalKmh, float emergencyKmh)
        {
            var context = CreateProtectionContext(mode, out _);
            context.Settings.orpMinimumTargetMarginM = margin;
            context.Settings.serviceStopMarginM = margin;
            context.Settings.orpSpeedLimitKmh = 18f;
            TrainAtcLogic.Calculate(context);
            var pattern = context.State.brakePattern;
            Assert.That(pattern.normalPattern[targetIndex - 1].speedLimitMps, Is.EqualTo(20f));
            Assert.That(pattern.emergencyPattern[targetIndex - 1].speedLimitMps, Is.EqualTo(82f / 3.6f).Within(0.0001f));
            Assert.That(pattern.normalPattern[targetIndex].speedLimitMps, Is.EqualTo(normalKmh / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[targetIndex].speedLimitMps, Is.EqualTo(emergencyKmh / 3.6f).Within(0.0001f));
            Assert.That(pattern.normalPattern[40].speedLimitMps, Is.EqualTo(normalKmh / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[39].speedLimitMps, Is.Zero);
            Assert.That(pattern.emergencyPattern[40].speedLimitMps, Is.Zero);
        }

        [TestCase(OverrunProtectionMode.Restricted)]
        [TestCase(OverrunProtectionMode.None)]
        public void ProtectionStartsAtEarlierSampleWhenTargetFallsBetweenSamples(OverrunProtectionMode mode)
        {
            var context = CreateProtectionContext(mode, out _);
            context.Settings.orpMinimumTargetMarginM = 101f;
            context.Settings.serviceStopMarginM = 101f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern[18].speedLimitMps, Is.EqualTo(20f));
            float expectedSpeed = 0f;
            if (mode == OverrunProtectionMode.Restricted) expectedSpeed = 25f / 3.6f;
            Assert.That(context.State.brakePattern.normalPattern[19].speedLimitMps, Is.EqualTo(expectedSpeed).Within(0.0001f));
        }

        [TestCase(OverrunProtectionMode.Restricted)]
        [TestCase(OverrunProtectionMode.None)]
        public void ShortPathAppliesProtectionFromPathStart(OverrunProtectionMode mode)
        {
            var context = CreateBrakePatternContext(out var route);
            route.overrunProtectionMode = mode;
            TrainAtcLogic.Calculate(context);
            float expectedNormalKmh = 0f;
            float expectedEmergencyKmh = 82f;
            if (mode == OverrunProtectionMode.Restricted)
            {
                expectedNormalKmh = 25f;
                expectedEmergencyKmh = 35f;
            }
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps),
                Is.All.EqualTo(expectedNormalKmh / 3.6f).Within(0.0001f));
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps),
                Is.EqualTo(new[] { expectedEmergencyKmh / 3.6f, expectedEmergencyKmh / 3.6f, 0f, 0f }).Within(0.0001f));
        }

        [Test]
        public void OrpKeepsLowerPermanentLimitsAndTheirEmergencyMargin()
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Restricted, out _);
            context.atcEdgesById["front"].speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 110f, endDistanceOnAtcEdgeM = 140f, speedLimitKmh = 18f });
            TrainAtcLogic.Calculate(context);
            var pattern = context.State.brakePattern;
            Assert.That(pattern.normalPattern[21].speedLimitMps, Is.EqualTo(25f / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[21].speedLimitMps, Is.EqualTo(35f / 3.6f).Within(0.0001f));
            Assert.That(pattern.normalPattern[22].speedLimitMps, Is.EqualTo(5f));
            Assert.That(pattern.emergencyPattern[22].speedLimitMps, Is.EqualTo(28f / 3.6f).Within(0.0001f));
            Assert.That(pattern.normalPattern[29].speedLimitMps, Is.EqualTo(25f / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[29].speedLimitMps, Is.EqualTo(35f / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[39].speedLimitMps, Is.Zero);
        }

        [Test]
        public void OrpDoesNotRaiseLowerLineMaximumAndEmergencyMaximum()
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Restricted, out _);
            context.Settings.maximumOperatingSpeedKmh = 18f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.All.EqualTo(5f));
            Assert.That(context.State.brakePattern.emergencyPattern[20].speedLimitMps, Is.EqualTo(28f / 3.6f).Within(0.0001f));
        }

        [TestCase(OverrunProtectionMode.Restricted)]
        [TestCase(OverrunProtectionMode.Normal)]
        [TestCase(OverrunProtectionMode.None)]
        public void OneSegmentPathSetsBothEmergencySamplesToZero(OverrunProtectionMode mode)
        {
            var context = CreateBrakePatternContext(out var route);
            route.overrunProtectionMode = mode;
            context.Settings.maximumSamplingIntervalM = 20f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(new[] { 0f, 0f }));
        }

        [TestCase("orpNegativeMargin")]
        [TestCase("orpNanMargin")]
        [TestCase("orpInfiniteMargin")]
        [TestCase("orpNegativeSpeed")]
        [TestCase("orpNanSpeed")]
        [TestCase("orpInfiniteSpeed")]
        [TestCase("noneNegativeMargin")]
        [TestCase("noneNanMargin")]
        [TestCase("noneInfiniteMargin")]
        [TestCase("unknownMode")]
        public void InvalidProtectionSettingsClearPreviousPattern(string condition)
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Restricted, out var route);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Not.Empty);
            switch (condition)
            {
                case "orpNegativeMargin": context.Settings.orpMinimumTargetMarginM = -1f; break;
                case "orpNanMargin": context.Settings.orpMinimumTargetMarginM = float.NaN; break;
                case "orpInfiniteMargin": context.Settings.orpMinimumTargetMarginM = float.PositiveInfinity; break;
                case "orpNegativeSpeed": context.Settings.orpSpeedLimitKmh = -1f; break;
                case "orpNanSpeed": context.Settings.orpSpeedLimitKmh = float.NaN; break;
                case "orpInfiniteSpeed": context.Settings.orpSpeedLimitKmh = float.PositiveInfinity; break;
                case "noneNegativeMargin": route.overrunProtectionMode = OverrunProtectionMode.None; context.Settings.serviceStopMarginM = -1f; break;
                case "noneNanMargin": route.overrunProtectionMode = OverrunProtectionMode.None; context.Settings.serviceStopMarginM = float.NaN; break;
                case "noneInfiniteMargin": route.overrunProtectionMode = OverrunProtectionMode.None; context.Settings.serviceStopMarginM = float.PositiveInfinity; break;
                case "unknownMode": route.overrunProtectionMode = (OverrunProtectionMode)99; break;
            }
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
        }

        private static TrainAtcContext CreateProtectionContext(
            OverrunProtectionMode mode, out TrackCircuitAtcRouteInfomation route)
        {
            var context = CreateBrakePatternContext(out route);
            route.overrunProtectionMode = mode;
            context.atcEdgesById["front"].lengthM = 200f;
            context.atcEdgesById["front"].gradientProfiles[1].distanceOnAtcEdgeM = 200f;
            return context;
        }

        [TestCase(OverrunProtectionMode.Restricted, 0f, false)]
        [TestCase(OverrunProtectionMode.Restricted, 50f, false)]
        [TestCase(OverrunProtectionMode.Restricted, 150f, false)]
        [TestCase(OverrunProtectionMode.Restricted, 190f, true)]
        [TestCase(OverrunProtectionMode.Normal, 50f, false)]
        [TestCase(OverrunProtectionMode.None, 50f, false)]
        public void ShowsOrpOnlyWhileApproachingEmergencyPattern(
            OverrunProtectionMode mode, float distanceOnEdgeM, bool expected)
        {
            var context = CreateProtectionContext(mode, out _);
            context.State.frontPosition.distanceOnAtcEdgeM = distanceOnEdgeM;
            context.Input.signedSpeedMps = 20f;

            TrainAtcLogic.Calculate(context);

            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.Output.isOrpActive, Is.EqualTo(expected));
            Assert.That(context.Output.normalAllowSpeedMps,
                Is.EqualTo(context.State.brakePattern.normalAllowSpeedMps));
        }

        [TestCase(0f)]
        [TestCase(34f)]
        [TestCase(35f)]
        [TestCase(36f)]
        [TestCase(-35f)]
        public void OrpApproachDoesNotDependOnMeasuredSpeed(float speedKmh)
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Restricted, out _);
            context.State.frontPosition.distanceOnAtcEdgeM = 190f;
            context.Input.signedSpeedMps = speedKmh / 3.6f;

            TrainAtcLogic.Calculate(context);

            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.Output.isPatternApproaching, Is.False);
            Assert.That(context.Output.isOrpActive, Is.True);
            Assert.That(context.State.brakePattern.emergencyPatternTargetMps, Is.Zero);
            Assert.That(context.State.brakePattern.normalAllowSpeedMps, Is.EqualTo(25f / 3.6f));
        }

        [TestCase(50f, 35f, false)]
        [TestCase(190f, 0f, true)]
        public void ShowsOrpOnlyWhenEmergencyApproachTargetsAStop(
            float distanceOnEdgeM, float expectedTargetSpeedKmh, bool expectedOrp)
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Restricted, out _);
            context.State.frontPosition.distanceOnAtcEdgeM = distanceOnEdgeM;

            TrainAtcLogic.Calculate(context);

            var pattern = context.State.brakePattern;
            Assert.That(pattern.hasValidPattern, Is.True);
            Assert.That(pattern.isEmergencyPatternApproachSection, Is.True);
            Assert.That(pattern.emergencyPatternTargetMps, Is.EqualTo(expectedTargetSpeedKmh / 3.6f));
            Assert.That(context.Output.isOrpActive, Is.EqualTo(expectedOrp));
        }

        [TestCase(false, 0f, 0f)]
        [TestCase(false, 5f, 5f)]
        [TestCase(false, 12f, 12f)]
        [TestCase(true, 0f, 12f)]
        [TestCase(true, 5f, 7f)]
        [TestCase(true, 12f, 0f)]
        public void ConvertsFirstEdgePositionWithoutGradientOrSpeedData(
            bool reverse, float edgeDistance, float expectedPathDistance)
        {
            var context = CreateBrakePatternContext(out var route);
            if (reverse)
            {
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            }
            TrainAtcLogic.Calculate(context);
            context.atcEdgesById["front"].gradientProfiles = null;
            context.atcEdgesById["front"].speedLimitSections = null;

            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "front", edgeDistance,
                out float distanceOnPathM), Is.True);
            Assert.That(distanceOnPathM, Is.EqualTo(expectedPathDistance));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothEdgePositionsAtJunctionReturnSamePathDistance(bool entersAtNodeB)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            float nextEntryDistance = 0f;
            if (entersAtNodeB)
            {
                context.atcEdgesById["next"].atcNodeAId = "n2";
                context.atcEdgesById["next"].atcNodeBId = "n1";
                nextEntryDistance = 9f;
            }
            TrainAtcLogic.Calculate(context);

            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "front", 12f,
                out float previousDistance), Is.True);
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "next", nextEntryDistance,
                out float nextDistance), Is.True);
            Assert.That(previousDistance, Is.EqualTo(12f));
            Assert.That(nextDistance, Is.EqualTo(previousDistance));
        }

        [Test]
        public void ReversedFirstEdgeDeterminesFollowingEdgeDirection()
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("rear");
            route.stopAtcEdgeId = "rear";
            context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            TrainAtcLogic.Calculate(context);

            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "rear", 20f,
                out float distanceOnPathM), Is.True);
            Assert.That(distanceOnPathM, Is.EqualTo(52f));
        }

        [TestCase(null)]
        [TestCase(" ")]
        [TestCase("missing")]
        public void MissingEdgeIdDoesNotReturnPathDistance(string edgeId)
        {
            var context = CreateBrakePatternContext(out var route);
            TrainAtcLogic.Calculate(context);
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, edgeId, 5f,
                out float distanceOnPathM), Is.False);
            Assert.That(distanceOnPathM, Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(13f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidEdgePositionDoesNotReturnPathDistance(float edgeDistance)
        {
            var context = CreateBrakePatternContext(out var route);
            TrainAtcLogic.Calculate(context);
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "front", edgeDistance,
                out float distanceOnPathM), Is.False);
            Assert.That(distanceOnPathM, Is.Zero);
        }

        [TestCase("nullRoute")]
        [TestCase("nullPath")]
        [TestCase("emptyPath")]
        [TestCase("wrongFirstEdge")]
        [TestCase("missingTarget")]
        [TestCase("duplicateTarget")]
        [TestCase("missingPreviousEdge")]
        [TestCase("invalidLength")]
        [TestCase("overflow")]
        [TestCase("disconnected")]
        [TestCase("ambiguous")]
        [TestCase("unspecifiedDirection")]
        [TestCase("unknownPosition")]
        public void UnresolvableEdgePositionDoesNotReturnPartialPathDistance(string condition)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            TrainAtcLogic.Calculate(context);
            var position = context.State.currentPosition;
            switch (condition)
            {
                case "nullRoute": route = null; break;
                case "nullPath": route.atcEdgePath = null; break;
                case "emptyPath": route.atcEdgePath.Clear(); break;
                case "wrongFirstEdge": route.atcEdgePath.RemoveAt(0); break;
                case "missingTarget": route.atcEdgePath.RemoveAt(1); break;
                case "duplicateTarget": route.atcEdgePath.Add("next"); break;
                case "missingPreviousEdge": context.atcEdgesById.Remove("front"); break;
                case "invalidLength": context.atcEdgesById["next"].lengthM = 0f; break;
                case "overflow":
                    context.atcEdgesById["front"].lengthM = float.MaxValue;
                    context.atcEdgesById["next"].lengthM = float.MaxValue;
                    break;
                case "disconnected": context.atcEdgesById["next"].atcNodeAId = "other"; break;
                case "ambiguous": context.atcEdgesById["next"].atcNodeBId = "n1"; break;
                case "unspecifiedDirection": context.State.currentTravelDirection = TrackAtcTravelDirection.Unspecified; break;
                case "unknownPosition": context.State.hasCurrentPosition = false; break;
            }

            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, "next", 1f,
                out float distanceOnPathM), Is.False);
            Assert.That(distanceOnPathM, Is.Zero);
            Assert.That(context.State.currentPosition, Is.EqualTo(position));
            Assert.That(context.State.brakePattern.pathLengthM, Is.EqualTo(21f));
        }

        [TestCase(false, 0f, 0f, 10f, 10f)]
        [TestCase(false, 4f, 4f, 14f, 10f)]
        [TestCase(false, 6f, 6f, 18f, 15f)]
        [TestCase(false, 12f, 12f, 30f, 15f)]
        [TestCase(true, 0f, 12f, -30f, 15f)]
        [TestCase(true, 6f, 6f, -18f, 15f)]
        [TestCase(true, 8f, 4f, -14f, 10f)]
        [TestCase(true, 12f, 0f, -10f, 10f)]
        public void ReadsPathPointUsingEntryOriginAndTravelDirection(
            bool reverse, float pathDistance, float edgeDistance, float gradient, float speedLimit)
        {
            var context = CreateBrakePatternContext(out var route);
            var edge = context.atcEdgesById["front"];
            edge.trackCircuitId = "front-circuit";
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 0f, gradientPermille = 10f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 4f, gradientPermille = 14f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 12f, gradientPermille = 30f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 4f, speedLimitKmh = 36f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 4f, endDistanceOnAtcEdgeM = 12f, speedLimitKmh = 54f });
            if (reverse)
            {
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            }
            TrainAtcLogic.Calculate(context);
            var position = context.State.currentPosition;

            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route, pathDistance, out var information),
                Is.True);
            Assert.That(information.atcEdgeId, Is.EqualTo("front"));
            Assert.That(information.trackCircuitId, Is.EqualTo("front-circuit"));
            Assert.That(information.distanceOnPathM, Is.EqualTo(pathDistance));
            Assert.That(information.distanceOnAtcEdgeM, Is.EqualTo(edgeDistance));
            Assert.That(information.direction, Is.EqualTo(context.State.currentTravelDirection));
            Assert.That(information.gradientPermille, Is.EqualTo(gradient).Within(0.0001f));
            Assert.That(information.speedLimitMps, Is.EqualTo(speedLimit).Within(0.0001f));
            Assert.That(context.State.currentPosition, Is.EqualTo(position));
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, information.atcEdgeId,
                information.distanceOnAtcEdgeM, out float restoredDistance), Is.True);
            Assert.That(restoredDistance, Is.EqualTo(pathDistance).Within(0.0001f));
        }

        [TestCase(false, 12f, 0f, 0f, 10f)]
        [TestCase(false, 15f, 3f, 6f, 10f)]
        [TestCase(false, 21f, 9f, 18f, 20f)]
        [TestCase(true, 12f, 9f, -18f, 20f)]
        [TestCase(true, 15f, 6f, -12f, 20f)]
        [TestCase(true, 21f, 0f, 0f, 10f)]
        public void ReadsFollowingEdgeIncludingJunctionAndPathEnd(
            bool entersAtNodeB, float pathDistance, float edgeDistance, float gradient, float speedLimit)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            var edge = context.atcEdgesById["next"];
            if (entersAtNodeB)
            {
                edge.atcNodeAId = "n2";
                edge.atcNodeBId = "n1";
            }
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 0f, gradientPermille = 0f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 9f, gradientPermille = 18f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 3f, speedLimitKmh = 36f });
            TrainAtcLogic.Calculate(context);

            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route, pathDistance, out var information),
                Is.True);
            Assert.That(information.atcEdgeId, Is.EqualTo("next"));
            Assert.That(information.distanceOnAtcEdgeM, Is.EqualTo(edgeDistance));
            Assert.That(information.gradientPermille, Is.EqualTo(gradient).Within(0.0001f));
            Assert.That(information.speedLimitMps, Is.EqualTo(speedLimit).Within(0.0001f));
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, information.atcEdgeId,
                information.distanceOnAtcEdgeM, out float restoredDistance), Is.True);
            Assert.That(restoredDistance, Is.EqualTo(pathDistance).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PathEndpointUsesExactEdgeEndDespiteFloatAddition(bool entersAtNodeB)
        {
            var context = CreateBrakePatternContext(out var route);
            context.atcEdgesById["front"].lengthM = 10.1f;
            context.atcEdgesById["front"].gradientProfiles[1].distanceOnAtcEdgeM = 10.1f;
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            var edge = context.atcEdgesById["next"];
            edge.lengthM = 0.3f;
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 0f, gradientPermille = 10f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = 0.3f, gradientPermille = 20f });
            float expectedDistance = edge.lengthM;
            float expectedGradient = 20f;
            if (entersAtNodeB)
            {
                edge.atcNodeAId = "n2";
                edge.atcNodeBId = "n1";
                expectedDistance = 0f;
                expectedGradient = -10f;
            }
            TrainAtcLogic.Calculate(context);

            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route,
                context.State.brakePattern.pathLengthM, out var information), Is.True);
            Assert.That(information.distanceOnAtcEdgeM, Is.EqualTo(expectedDistance));
            Assert.That(information.gradientPermille, Is.EqualTo(expectedGradient));
            Assert.That(TrainAtcLogic.TryGetPathDistance(context, route, information.atcEdgeId,
                information.distanceOnAtcEdgeM, out float restoredDistance), Is.True);
            Assert.That(restoredDistance, Is.EqualTo(context.State.brakePattern.pathLengthM));
        }

        [TestCase(4f, 20f)]
        [TestCase(5.5f, 10f)]
        [TestCase(7f, 20f)]
        public void PointSpeedUsesGraphSectionsInsteadOfExpandedPatternSamples(float distance, float speedLimit)
        {
            var context = CreateBrakePatternContext(out var route);
            var edge = context.atcEdgesById["front"];
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 12f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 12f, speedLimitKmh = 90f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 5f, endDistanceOnAtcEdgeM = 6f, speedLimitKmh = 36f });
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern[1].speedLimitMps, Is.EqualTo(10f));

            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route, distance, out var information), Is.True);
            Assert.That(information.speedLimitMps, Is.EqualTo(speedLimit));
        }

        [TestCase(-1f)]
        [TestCase(13f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPathDistanceDoesNotReturnPointInformation(float distance)
        {
            var context = CreateBrakePatternContext(out var route);
            var edge = context.atcEdgesById["front"];
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 12f });
            TrainAtcLogic.Calculate(context);
            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route, distance, out var information), Is.False);
            Assert.That(information, Is.EqualTo(default(TrainAtcPathPointInformation)));
        }

        [TestCase("nullRoute")]
        [TestCase("nullPath")]
        [TestCase("emptyPath")]
        [TestCase("wrongFirstEdge")]
        [TestCase("missingEdge")]
        [TestCase("disconnected")]
        [TestCase("ambiguous")]
        [TestCase("unspecifiedDirection")]
        [TestCase("unknownPosition")]
        [TestCase("missingGradient")]
        [TestCase("unorderedGradient")]
        [TestCase("nanGradient")]
        [TestCase("missingEndpoint")]
        [TestCase("invalidSpeed")]
        public void UnresolvablePointDoesNotReturnPartialInformation(string condition)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            var edge = context.atcEdgesById["next"];
            edge.gradientProfiles.Clear();
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
            edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 9f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 9f, speedLimitKmh = 36f });
            TrainAtcLogic.Calculate(context);
            switch (condition)
            {
                case "nullRoute": route = null; break;
                case "nullPath": route.atcEdgePath = null; break;
                case "emptyPath": route.atcEdgePath.Clear(); break;
                case "wrongFirstEdge": route.atcEdgePath.RemoveAt(0); break;
                case "missingEdge": route.atcEdgePath[1] = "missing"; break;
                case "disconnected": edge.atcNodeAId = "other"; break;
                case "ambiguous": edge.atcNodeBId = "n1"; break;
                case "unspecifiedDirection": context.State.currentTravelDirection = TrackAtcTravelDirection.Unspecified; break;
                case "unknownPosition": context.State.hasCurrentPosition = false; break;
                case "missingGradient": edge.gradientProfiles.Clear(); break;
                case "unorderedGradient": edge.gradientProfiles.Reverse(); break;
                case "nanGradient": edge.gradientProfiles[1].gradientPermille = float.NaN; break;
                case "missingEndpoint": edge.gradientProfiles[1].distanceOnAtcEdgeM = 8f; break;
                case "invalidSpeed": edge.speedLimitSections[0].speedLimitKmh = -1f; break;
            }

            Assert.That(TrainAtcLogic.TryGetPathPointInformation(context, route, 13f, out var information), Is.False);
            Assert.That(information, Is.EqualTo(default(TrainAtcPathPointInformation)));
            Assert.That(context.State.brakePattern.pathLengthM, Is.EqualTo(21f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AppliesPermanentLimitsInSelectedTravelDirection(bool reverse)
        {
            var context = CreateBrakePatternContext(out var route);
            var edge = context.atcEdgesById["front"];
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 4f, speedLimitKmh = 36f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 4f, endDistanceOnAtcEdgeM = 12f, speedLimitKmh = 54f });
            float[] expected = { 10f, 10f, 0f, 0f };
            float[] expectedEmergency = { 46f / 3.6f, 46f / 3.6f, 0f, 0f };
            if (reverse)
            {
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
                context.Input.frontTelegram.atcRouteInfomation.Clear();
                context.Input.frontTelegram.atcRouteInfomation.Add(("front", TrackAtcTravelDirection.BtoA), route);
                expected = new[] { 15f, 15f, 0f, 0f };
                expectedEmergency = new[] { 64f / 3.6f, 64f / 3.6f, 0f, 0f };
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expected));
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expectedEmergency).Within(0.0001f));
        }

        [Test]
        public void OverlappingPermanentLimitsTakeLowestSpeedWithoutRaisingLineMaximum()
        {
            var context = CreateBrakePatternContext(out _);
            context.Settings.maximumSamplingIntervalM = 2f;
            var edge = context.atcEdgesById["front"];
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 12f, speedLimitKmh = 90f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 4f, endDistanceOnAtcEdgeM = 8f, speedLimitKmh = 36f });
            edge.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 8f, endDistanceOnAtcEdgeM = 12f, speedLimitKmh = 18f });
            TrainAtcLogic.Calculate(context);
            float[] expected = { 20f, 20f, 10f, 10f, 5f, 0f, 0f };
            float[] expectedEmergency = { 82f / 3.6f, 82f / 3.6f, 46f / 3.6f, 46f / 3.6f, 28f / 3.6f, 0f, 0f };
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expected));
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expectedEmergency).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolvesFollowingEdgeDirectionAndAppliesPathDistanceOffset(bool entersAtNodeB)
        {
            var context = CreateBrakePatternContext(out var route);
            context.Settings.maximumSamplingIntervalM = 3f;
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            var next = context.atcEdgesById["next"];
            next.speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 6f, speedLimitKmh = 36f });
            float[] expected = { 20f, 20f, 20f, 20f, 10f, 10f, 0f, 0f };
            if (entersAtNodeB)
            {
                next.atcNodeAId = "n2";
                next.atcNodeBId = "n1";
                expected = new[] { 20f, 20f, 20f, 20f, 20f, 10f, 0f, 0f };
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expected));
            for (int i = 0; i < expected.Length; i++)
            {
                float emergencySpeed = 0f;
                if (i < expected.Length - 2) emergencySpeed = expected[i] + 10f / 3.6f;
                Assert.That(context.State.brakePattern.emergencyPattern[i].speedLimitMps, Is.EqualTo(emergencySpeed).Within(0.0001f));
            }
        }

        [Test]
        public void ShortLimitBetweenSamplesIsAppliedToBothSurroundingSamples()
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            context.atcEdgesById["front"].speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 5f, endDistanceOnAtcEdgeM = 6f, speedLimitKmh = 36f });
            TrainAtcLogic.Calculate(context);
            float[] expected = { 20f, 10f, 10f, 20f, 0f, 0f };
            float[] expectedEmergency = { 82f / 3.6f, 46f / 3.6f, 46f / 3.6f, 82f / 3.6f, 0f, 0f };
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expected));
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expectedEmergency).Within(0.0001f));
        }

        [Test]
        public void ZeroPermanentSpeedLimitUsesEmergencyMarginBeforeStopTarget()
        {
            var context = CreateBrakePatternContext(out _);
            context.atcEdgesById["front"].speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 4f, endDistanceOnAtcEdgeM = 8f, speedLimitKmh = 0f });
            TrainAtcLogic.Calculate(context);
            float[] expected = { 20f, 0f, 0f, 0f };
            float[] expectedEmergency = { 82f / 3.6f, 10f / 3.6f, 0f, 0f };
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expected));
            Assert.That(context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(expectedEmergency).Within(0.0001f));
        }

        [TestCase("nullList")]
        [TestCase("nullSection")]
        [TestCase("negativeStart")]
        [TestCase("reversedRange")]
        [TestCase("outsideEdge")]
        [TestCase("nanSpeed")]
        [TestCase("negativeSpeed")]
        public void InvalidPermanentLimitClearsPreviousPattern(string condition)
        {
            var context = CreateBrakePatternContext(out _);
            var edge = context.atcEdgesById["front"];
            var section = new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 4f, endDistanceOnAtcEdgeM = 8f, speedLimitKmh = 36f };
            edge.speedLimitSections.Add(section);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Not.Empty);
            switch (condition)
            {
                case "nullList": edge.speedLimitSections = null; break;
                case "nullSection": edge.speedLimitSections.Add(null); break;
                case "negativeStart": section.startDistanceOnAtcEdgeM = -1f; break;
                case "reversedRange": section.endDistanceOnAtcEdgeM = 2f; break;
                case "outsideEdge": section.endDistanceOnAtcEdgeM = 13f; break;
                case "nanSpeed": section.speedLimitKmh = float.NaN; break;
                case "negativeSpeed": section.speedLimitKmh = -1f; break;
            }
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
            Assert.That(context.State.isHealthy, Is.True);
        }

        [TestCase("disconnected")]
        [TestCase("ambiguous")]
        [TestCase("wrongFirstEdge")]
        public void UnresolvablePathDirectionClearsPreviousPattern(string condition)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Not.Empty);
            switch (condition)
            {
                case "disconnected": context.atcEdgesById["next"].atcNodeAId = "other"; break;
                case "ambiguous": context.atcEdgesById["next"].atcNodeBId = "n1"; break;
                case "wrongFirstEdge": route.atcEdgePath.RemoveAt(0); break;
            }
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
        }

        [TestCase(5f, 4, 4f)]
        [TestCase(3f, 5, 3f)]
        [TestCase(20f, 2, 12f)]
        public void InitializesBrakePatternUsingConfiguredInterval(
            float maximumInterval, int sampleCount, float interval)
        {
            var context = CreateBrakePatternContext(out _);
            context.Settings.maximumSamplingIntervalM = maximumInterval;
            TrainAtcLogic.Calculate(context);

            var pattern = context.State.brakePattern;
            Assert.That(pattern.pathLengthM, Is.EqualTo(12f));
            Assert.That(pattern.samplingIntervalM, Is.EqualTo(interval));
            Assert.That(pattern.samplingIntervalM * (sampleCount - 1), Is.EqualTo(12f));
            Assert.That(pattern.pathAtcEdges, Is.EqualTo(new[] { "front" }));
            Assert.That(pattern.normalPattern.Count, Is.EqualTo(sampleCount));
            Assert.That(pattern.emergencyPattern.Count, Is.EqualTo(sampleCount));
            for (int i = 0; i < sampleCount; i++)
            {
                float normalSpeed = 0f;
                float emergencySpeed = 0f;
                if (i < sampleCount - 2)
                {
                    normalSpeed = 20f;
                    emergencySpeed = 82f / 3.6f;
                }
                Assert.That(pattern.normalPattern[i].speedLimitMps, Is.EqualTo(normalSpeed));
                Assert.That(pattern.emergencyPattern[i].speedLimitMps, Is.EqualTo(emergencySpeed).Within(0.0001f));
            }
        }

        [Test]
        public void CopiesWholePathAndReplacesPreviousSamples()
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            TrainAtcLogic.Calculate(context);
            var pattern = context.State.brakePattern;
            Assert.That(pattern.pathLengthM, Is.EqualTo(21f));
            Assert.That(pattern.samplingIntervalM, Is.EqualTo(4.2f).Within(0.0001f));
            Assert.That(pattern.normalPattern.Count, Is.EqualTo(6));
            Assert.That(pattern.pathAtcEdges, Is.EqualTo(new[] { "front", "next" }));

            context.State.currentTelegram.atcRouteInfomation[("front", TrackAtcTravelDirection.AtoB)]
                .atcEdgePath.Clear();
            Assert.That(pattern.pathAtcEdges, Is.EqualTo(new[] { "front", "next" }));

            route.atcEdgePath.RemoveAt(1);
            route.stopAtcEdgeId = "front";
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern, Is.SameAs(pattern));
            Assert.That(pattern.pathLengthM, Is.EqualTo(12f));
            Assert.That(pattern.pathAtcEdges, Is.EqualTo(new[] { "front" }));
            Assert.That(pattern.normalPattern.Count, Is.EqualTo(4));
            Assert.That(pattern.emergencyPattern.Count, Is.EqualTo(4));
        }

        [TestCase("nullRoute")]
        [TestCase("nullPath")]
        [TestCase("emptyPath")]
        [TestCase("missingEdge")]
        [TestCase("nullEdgeId")]
        public void FailedPathClearsPreviousBrakePattern(string condition)
        {
            var context = CreateBrakePatternContext(out var route);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Not.Empty);
            switch (condition)
            {
                case "nullRoute":
                    context.Input.frontTelegram.atcRouteInfomation[("front", TrackAtcTravelDirection.AtoB)] = null;
                    break;
                case "nullPath": route.atcEdgePath = null; break;
                case "emptyPath": route.atcEdgePath.Clear(); break;
                case "missingEdge": route.atcEdgePath.Add("missing"); break;
                case "nullEdgeId": route.atcEdgePath.Add(null); break;
            }

            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
            Assert.That(context.State.isHealthy, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSamplingIntervalClearsPreviousBrakePattern(float interval)
        {
            var context = CreateBrakePatternContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Settings.maximumSamplingIntervalM = interval;
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPathEdgeLengthClearsPreviousBrakePattern(float length)
        {
            var context = CreateBrakePatternContext(out var route);
            route.atcEdgePath.Add("next");
            route.stopAtcEdgeId = "next";
            TrainAtcLogic.Calculate(context);
            context.atcEdgesById["next"].lengthM = length;
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
        }

        [TestCase("missing")]
        [TestCase("invalid")]
        [TestCase("missingKey")]
        public void UnavailableTelegramKeepsPreviousBrakePatternDuringGrace(string condition)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            var samples = context.State.brakePattern.normalPattern.ToArray();
            switch (condition)
            {
                case "missing": context.Input.frontTelegram = null; break;
                case "invalid": context.Input.frontTelegram.isValid = false; break;
                case "missingKey": context.Input.frontTelegram.atcRouteInfomation.Clear(); break;
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
            Assert.That(context.State.brakePattern.normalPattern, Is.EqualTo(samples));
            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.State.brake.isEmergencyRequired, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.False);
            Assert.That(context.State.isHealthy, Is.True);
        }

        [Test]
        public void NoSignalRequiresEmergencyOnlyAfterTimeoutIsExceeded()
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Input.frontTelegram = null;
            for (int i = 0; i < 4; i++)
            {
                TrainAtcLogic.Calculate(context);
                Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo((i + 1) * 0.25f));
                Assert.That(context.Output.hasValidPattern, Is.True);
                Assert.That(context.Output.brake.isEmergency, Is.False);
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.GreaterThan(context.Settings.noSignalTimeoutSeconds));
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.State.brake.isEmergencyRequired, Is.True);
            Assert.That(context.State.brake.isEmergencyHold, Is.True);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [Test]
        public void SignalRecoveryResetsContinuousTimerBeforeNextOutage()
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            var telegram = context.Input.frontTelegram;
            context.Input.frontTelegram = null;
            TrainAtcLogic.Calculate(context);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.5f));

            context.Input.frontTelegram = telegram;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.Zero);
            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.Output.brake.isEmergency, Is.False);

            context.Input.frontTelegram = null;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
            Assert.That(context.Output.brake.isEmergency, Is.False);
        }

        [Test]
        public void TimeoutEmergencyRemainsHeldAfterSignalReturnsUntilStoppedInEmergencyPosition()
        {
            var context = CreateNoSignalContext(out _);
            context.Input.signedSpeedMps = 1f;
            TrainAtcLogic.Calculate(context);
            var telegram = context.Input.frontTelegram;
            context.Input.frontTelegram = null;
            for (int i = 0; i < 5; i++) TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.brake.isEmergency, Is.True);

            context.Input.frontTelegram = telegram;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.Zero);
            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.State.brake.isEmergencyRequired, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);

            context.Input.signedSpeedMps = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.brake.isEmergency, Is.True);
            context.Input.cab.isEmergencyBrake = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brake.isEmergencyHold, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.False);
        }

        [Test]
        public void MissingInitialTelegramRequiresEmergencyWithoutGrace()
        {
            var context = CreateNoSignalContext(out _);
            context.Input.frontTelegram = null;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
            Assert.That(context.State.noSignalElapsedSeconds, Is.LessThan(context.Settings.noSignalTimeoutSeconds));
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        public void ZeroTimeoutRequiresImmediateEmergencyEvenWithoutElapsedTime(float elapsedSeconds)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Settings.noSignalTimeoutSeconds = 0f;
            context.Input.deltaTimeSeconds = elapsedSeconds;
            context.Input.frontTelegram = null;
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidNoSignalTimeoutRequiresImmediateEmergency(float timeoutSeconds)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Settings.noSignalTimeoutSeconds = timeoutSeconds;
            TrainAtcLogic.Calculate(context);
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase("mass")]
        [TestCase("sampling")]
        [TestCase("speed")]
        [TestCase("brakeSettings")]
        [TestCase("cab")]
        [TestCase("time")]
        public void MissingSignalDoesNotGiveGraceForOtherInvalidInputs(string condition)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Input.frontTelegram = null;
            switch (condition)
            {
                case "mass": context.Input.hasCarMasses = false; break;
                case "sampling": context.Settings.maximumSamplingIntervalM = 0f; break;
                case "speed": context.Input.hasSpeedMeasurement = false; break;
                case "brakeSettings": context.Input.hasBrakeSettings = false; break;
                case "cab": context.Input.hasCabState = false; break;
                case "time": context.Input.deltaTimeSeconds = float.NaN; break;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase("cab")]
        [TestCase("reverse")]
        public void CabOrReverserChangeCannotReusePreviousCabPattern(string condition)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Input.frontTelegram = null;
            if (condition == "cab")
            {
                context.Input.cab.isFrontCab = false;
                context.Input.cab.carIndex = 1;
            }
            else
            {
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase("key")]
        [TestCase("neutral")]
        public void DisablingAtcClearsGraceAndCannotRestoreOldPattern(string condition)
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            context.Input.frontTelegram = null;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
            if (condition == "key") context.Input.cab.isKeyInserted = false;
            else context.Input.cab.reverserPosition = ReverserPosition.Neutral;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.Zero);
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.brake.isEmergency, Is.False);

            context.Input.cab.isKeyInserted = true;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void RetainedPatternUpdatesAllowedSpeedAcrossConnectedEdges(bool reverse, bool entersAtNodeB)
        {
            var context = CreateNoSignalContext(out var route);
            var front = context.State.frontPosition;
            front.distanceOnAtcEdgeM = 199.5f;
            string nextEdgeId = "next";
            float expectedEdgeDistance = 0.5f;
            float expectedPathDistance = 200.5f;
            TrackAtcTravelDirection expectedDirection = TrackAtcTravelDirection.AtoB;
            if (reverse)
            {
                front.distanceOnAtcEdgeM = 0.5f;
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
                context.Input.frontTelegram.atcRouteInfomation.Clear();
                context.Input.frontTelegram.atcRouteInfomation.Add(("front", TrackAtcTravelDirection.BtoA), route);
                nextEdgeId = "rear";
                expectedEdgeDistance = 59.5f;
                expectedDirection = TrackAtcTravelDirection.BtoA;
            }
            else
            {
                var next = context.atcEdgesById["next"];
                next.lengthM = 200f;
                next.gradientProfiles[1].distanceOnAtcEdgeM = 200f;
                if (entersAtNodeB)
                {
                    next.atcNodeAId = "n2";
                    next.atcNodeBId = "n1";
                    expectedEdgeDistance = 199.5f;
                    expectedDirection = TrackAtcTravelDirection.BtoA;
                }
            }
            route.atcEdgePath.Add(nextEdgeId);
            route.stopAtcEdgeId = nextEdgeId;
            Assert.That(TrainAtcLogic.TryCorrectPosition(context, front, context.State.rearPosition), Is.True);
            context.Input.deltaTimeSeconds = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.True);
            float previousAllowSpeed = context.Output.normalAllowSpeedMps;
            var samples = context.State.brakePattern.normalPattern.ToArray();

            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.25f;
            context.Input.signedSpeedMps = 4f;
            if (reverse) context.Input.signedSpeedMps = -4f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentPosition.atcEdgeId, Is.EqualTo(nextEdgeId));
            Assert.That(context.State.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(expectedEdgeDistance));
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(expectedDirection));
            Assert.That(context.State.brakePattern.normalPattern, Is.EqualTo(samples));
            Assert.That(context.Output.distanceOnPathM, Is.EqualTo(expectedPathDistance));
            Assert.That(context.Output.normalAllowSpeedMps, Is.LessThan(previousAllowSpeed));
            Assert.That(context.Output.hasValidPattern, Is.True);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
            Assert.That(context.Output.brake.isEmergency, Is.False);
        }

        [Test]
        public void GraceDoesNotContinueBeyondAcceptedPath()
        {
            var context = CreateNoSignalContext(out _);
            var front = context.State.frontPosition;
            front.distanceOnAtcEdgeM = 199.5f;
            Assert.That(TrainAtcLogic.TryCorrectPosition(context, front, context.State.rearPosition), Is.True);
            context.Input.deltaTimeSeconds = 0f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.Output.hasValidPattern, Is.True);

            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0.25f;
            context.Input.signedSpeedMps = 4f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentPosition.atcEdgeId, Is.EqualTo("next"));
            Assert.That(context.State.noSignalElapsedSeconds, Is.LessThan(context.Settings.noSignalTimeoutSeconds));
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [Test]
        public void ZeroElapsedTimeKeepsGraceTimerAndPositionUnchanged()
        {
            var context = CreateNoSignalContext(out _);
            TrainAtcLogic.Calculate(context);
            var position = context.State.currentPosition;
            context.Input.frontTelegram = null;
            context.Input.deltaTimeSeconds = 0f;
            for (int i = 0; i < 3; i++)
            {
                TrainAtcLogic.Calculate(context);
                Assert.That(context.State.noSignalElapsedSeconds, Is.Zero);
                Assert.That(context.State.currentPosition, Is.EqualTo(position));
                Assert.That(context.Output.hasValidPattern, Is.True);
                Assert.That(context.Output.brake.isEmergency, Is.False);
            }

            context.Input.deltaTimeSeconds = 0.25f;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.EqualTo(0.25f));
        }

        [TestCase("nullRoute")]
        [TestCase("emptyPath")]
        [TestCase("wrongFirstEdge")]
        [TestCase("missingEdge")]
        public void MalformedRouteWithCurrentKeyRequiresImmediateEmergency(string condition)
        {
            var context = CreateNoSignalContext(out var route);
            TrainAtcLogic.Calculate(context);
            switch (condition)
            {
                case "nullRoute":
                    context.Input.frontTelegram.atcRouteInfomation[("front", TrackAtcTravelDirection.AtoB)] = null;
                    break;
                case "emptyPath": route.atcEdgePath.Clear(); break;
                case "wrongFirstEdge": route.atcEdgePath[0] = "rear"; break;
                case "missingEdge": route.atcEdgePath.Add("missing"); break;
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.noSignalElapsedSeconds, Is.Zero);
            AssertClearedBrakePattern(context);
            Assert.That(context.Output.hasValidPattern, Is.False);
            Assert.That(context.Output.brake.isEmergency, Is.True);
        }

        [Test]
        public void SettingsCopyIncludesNoSignalTimeout()
        {
            var context = CreateNoSignalContext(out _);
            context.Settings.CopyFrom(new TrainAtcSettings { noSignalTimeoutSeconds = 2.5f });
            Assert.That(context.Settings.noSignalTimeoutSeconds, Is.EqualTo(2.5f));
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.EqualTo(72f));
        }

        private static TrainAtcContext CreateNoSignalContext(out TrackCircuitAtcRouteInfomation route)
        {
            var context = CreateProtectionContext(OverrunProtectionMode.Normal, out route);
            context.Settings.serviceDecelerationMps2 = 0.5f;
            context.Settings.emergencyDecelerationMps2 = 1.2f;
            context.Input.deltaTimeSeconds = 0.25f;
            return context;
        }

        private static TrainAtcContext CreateBrakePatternContext(out TrackCircuitAtcRouteInfomation route)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 72f };
            graph.atcEdge.Add(new TrackAtcGraphEdge
                { atcEdgeId = "front", lengthM = 12f, atcNodeAId = "n0", atcNodeBId = "n1" });
            graph.atcEdge.Add(new TrackAtcGraphEdge
                { atcEdgeId = "next", lengthM = 9f, atcNodeAId = "n1", atcNodeBId = "n2" });
            graph.atcEdge.Add(new TrackAtcGraphEdge
                { atcEdgeId = "rear", lengthM = 60f, atcNodeAId = "nr", atcNodeBId = "n0" });
            foreach (var edge in graph.atcEdge)
            {
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = 0f });
                edge.gradientProfiles.Add(new TrackAtcGradientProfile { distanceOnAtcEdgeM = edge.lengthM });
            }
            // 上限設定のテストでは、積分結果が速度上限を下回らない減速度を使う。
            context.Settings.serviceDecelerationMps2 = 1000f;
            context.Settings.emergencyDecelerationMps2 = 1000f;
            context.Input.hasSpeedMeasurement = true;
            context.Input.hasBrakeSettings = true;
            context.Input.brakeSettings.brakeSubstepCount = 1;
            context.Input.brakeSettings.maximumServiceBrakeStep = 7;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.AddRange(
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f });
            context.Input.hasCarMasses = true;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 30000f, centerDistanceFromFrontM = 10f });
            context.Input.frontReceiverDistanceFromFrontM = 10f;
            context.Input.rearReceiverDistanceFromFrontM = 10f;
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph,
                new TrainAtcPosition { atcEdgeId = "front", distanceOnAtcEdgeM = 5f, frontFacesAtoB = true },
                new TrainAtcPosition { atcEdgeId = "rear", distanceOnAtcEdgeM = 20f, frontFacesAtoB = true }), Is.True);
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput
            {
                isFrontCab = true,
                isKeyInserted = true,
                isInputEnabled = true,
                reverserPosition = ReverserPosition.Forward,
                isNeutral = true
            };
            route = new TrackCircuitAtcRouteInfomation
                { stopAtcEdgeId = "front", overrunProtectionMode = OverrunProtectionMode.Normal };
            route.atcEdgePath.Add("front");
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = true };
            context.Input.frontTelegram.atcRouteInfomation.Add(("front", TrackAtcTravelDirection.AtoB), route);
            return context;
        }

        private static void AssertClearedBrakePattern(TrainAtcContext context)
        {
            var pattern = context.State.brakePattern;
            Assert.That(pattern.hasValidPattern, Is.False);
            Assert.That(pattern.distanceOnPathM, Is.Zero);
            Assert.That(pattern.normalAllowSpeedMps, Is.Zero);
            Assert.That(pattern.emergencyAllowSpeedMps, Is.Zero);
            Assert.That(pattern.normalTargetSpeedMps, Is.Zero);
            Assert.That(pattern.emergencyPatternTargetMps, Is.Zero);
            Assert.That(pattern.isNormalDecelerationSection, Is.False);
            Assert.That(pattern.isEmergencyDecelerationSection, Is.False);
            Assert.That(pattern.isNormalPatternApproachSection, Is.False);
            Assert.That(pattern.isEmergencyPatternApproachSection, Is.False);
            Assert.That(pattern.samplingIntervalM, Is.Zero);
            Assert.That(pattern.pathLengthM, Is.Zero);
            Assert.That(pattern.pathAtcEdges, Is.Empty);
            Assert.That(pattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Empty);
            Assert.That(pattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.Empty);
        }

        [Test]
        public void KeepsCapturedCabStateUntilNextCalculationAndClearsMissingInput()
        {
            var context = CreateContext(false);
            context.Input.cab = new TrainAtcCabInput
            {
                carIndex = 7,
                isKeyInserted = true,
                isInputEnabled = true,
                powerPosition = 0,
                brakePosition = 8,
                serviceBrakePosition = 7,
                reverserPosition = ReverserPosition.Reverse,
                isNeutral = false,
                isEmergencyBrake = true
            };
            TrainAtcCabInput captured = context.Input.cab;
            TrainAtcLogic.Calculate(context);
            context.Input.cab = default;
            Assert.That(context.State.hasCabState, Is.True);
            Assert.That(context.State.cab, Is.EqualTo(captured));
            Assert.That(context.State.isHealthy, Is.True);
            Assert.That(context.State.isAtcPowerOn, Is.True);
            Assert.That(context.State.isAtcEnabled, Is.True);

            context.Input.hasCabState = false;
            context.Input.cab = captured;
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 12d };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.hasCabState, Is.False);
            Assert.That(context.State.cab, Is.EqualTo(default(TrainAtcCabInput)));
            Assert.That(context.State.isHealthy, Is.False);
            Assert.That(context.State.currentTelegram, Is.Null);
            Assert.That(context.State.isAtcPowerOn, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);

            context.Input.hasCabState = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isHealthy, Is.True);
            Assert.That(context.State.isAtcEnabled, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SelectsCabSideTelegramAndKeepsAnIndependentCopy(bool isFrontCab, bool valid)
        {
            var context = CreateContext(isFrontCab);
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = valid, issuedAtSeconds = 123.25d };
            context.Input.frontTelegram.atcRouteInfomation.Add(("one", TrackAtcTravelDirection.AtoB),
                new TrackCircuitAtcRouteInfomation { stopAtcEdgeId = "one" });
            context.Input.frontTelegram.atcRouteInfomation.Add(("one", TrackAtcTravelDirection.BtoA),
                new TrackCircuitAtcRouteInfomation { stopAtcEdgeId = "two" });
            context.Input.rearTelegram = new TrackCircuitAtcTelegram { isValid = valid, issuedAtSeconds = 124d };
            context.Input.rearTelegram.atcRouteInfomation.Add(("rear", TrackAtcTravelDirection.BtoA),
                new TrackCircuitAtcRouteInfomation { stopAtcEdgeId = "rear" });

            TrainAtcLogic.Calculate(context);
            context.Input.frontTelegram.atcRouteInfomation.Clear();
            context.Input.rearTelegram.atcRouteInfomation.Clear();
            context.Input.rearTelegram.issuedAtSeconds = 125d;
            Assert.That(context.State.currentTelegram.isValid, Is.EqualTo(valid));
            Assert.That(context.State.isHealthy, Is.True);
            if (isFrontCab)
            {
                Assert.That(context.State.currentTelegram.issuedAtSeconds, Is.EqualTo(123.25d));
                Assert.That(context.State.currentTelegram.atcRouteInfomation.Count, Is.EqualTo(2));
            }
            else
            {
                Assert.That(context.State.currentTelegram.issuedAtSeconds, Is.EqualTo(124d));
                Assert.That(context.State.currentTelegram.atcRouteInfomation.Count, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingSelectedTelegramClearsPreviousTelegramWithoutUsingOtherSide(bool isFrontCab)
        {
            var context = CreateContext(isFrontCab);
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = true };
            context.Input.rearTelegram = new TrackCircuitAtcTelegram { isValid = true };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTelegram, Is.Not.Null);
            if (isFrontCab)
            {
                context.Input.frontTelegram = null;
            }
            else
            {
                context.Input.rearTelegram = null;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTelegram, Is.Null);
            Assert.That(context.State.isHealthy, Is.True);
            Assert.That(context.State.isAtcEnabled, Is.True);
        }

        [Test]
        public void KeyOffIsHealthyAndDisablesAtcUntilKeyIsInsertedAgain()
        {
            var context = CreateContext(true);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isAtcEnabled, Is.True);
            context.Input.cab.isKeyInserted = false;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.hasCabState, Is.True);
            Assert.That(context.State.isHealthy, Is.True);
            Assert.That(context.State.isAtcPowerOn, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);
            context.Input.cab.isKeyInserted = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isAtcEnabled, Is.True);
        }

        [Test]
        public void CabChangeSelectsOtherSideWithoutDependingOnReverser()
        {
            var context = CreateContext(true);
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 10d };
            context.Input.rearTelegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 11d };
            context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTelegram.issuedAtSeconds, Is.EqualTo(10d));
            context.Input.cab.isFrontCab = false;
            context.Input.cab.carIndex = 7;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTelegram.issuedAtSeconds, Is.EqualTo(11d));
        }

        [Test]
        public void RearCabOnSingleCarStillSelectsRearTelegram()
        {
            var context = CreateContext(false);
            context.Input.cab.carIndex = 0;
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 10d };
            context.Input.rearTelegram = new TrackCircuitAtcTelegram { issuedAtSeconds = 11d };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTelegram.issuedAtSeconds, Is.EqualTo(11d));
        }

        [TestCase("carIndex")]
        [TestCase("frontCarIndex")]
        [TestCase("powerPosition")]
        [TestCase("brakePosition")]
        [TestCase("serviceBrakePosition")]
        [TestCase("serviceBrakeExceedsBrake")]
        [TestCase("powerAndBrake")]
        [TestCase("reverserPosition")]
        public void InvalidCabDisablesAtcAndClearsPreviousTelegram(string condition)
        {
            var context = CreateContext(true);
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = true };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isHealthy, Is.True);
            Assert.That(context.State.currentTelegram, Is.Not.Null);
            switch (condition)
            {
                case "carIndex": context.Input.cab.carIndex = -1; break;
                case "frontCarIndex": context.Input.cab.carIndex = 7; break;
                case "powerPosition": context.Input.cab.powerPosition = -1; break;
                case "brakePosition": context.Input.cab.brakePosition = -1; break;
                case "serviceBrakePosition": context.Input.cab.serviceBrakePosition = -1; break;
                case "serviceBrakeExceedsBrake": context.Input.cab.serviceBrakePosition = 9; break;
                case "powerAndBrake": context.Input.cab.powerPosition = 1; break;
                case "reverserPosition": context.Input.cab.reverserPosition = (ReverserPosition)2; break;
            }
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.hasCabState, Is.False);
            Assert.That(context.State.cab, Is.EqualTo(default(TrainAtcCabInput)));
            Assert.That(context.State.isHealthy, Is.False);
            Assert.That(context.State.isAtcPowerOn, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);
            Assert.That(context.State.currentTelegram, Is.Null);
        }

        [TestCase(true, true, ReverserPosition.Forward, TrackAtcTravelDirection.AtoB)]
        [TestCase(true, true, ReverserPosition.Reverse, TrackAtcTravelDirection.BtoA)]
        [TestCase(true, false, ReverserPosition.Forward, TrackAtcTravelDirection.BtoA)]
        [TestCase(true, false, ReverserPosition.Reverse, TrackAtcTravelDirection.AtoB)]
        [TestCase(false, true, ReverserPosition.Forward, TrackAtcTravelDirection.BtoA)]
        [TestCase(false, true, ReverserPosition.Reverse, TrackAtcTravelDirection.AtoB)]
        [TestCase(false, false, ReverserPosition.Forward, TrackAtcTravelDirection.AtoB)]
        [TestCase(false, false, ReverserPosition.Reverse, TrackAtcTravelDirection.BtoA)]
        public void StationaryTrainUsesReverserAndSelectedCabDirection(
            bool isFrontCab, bool frontFacesAtoB, ReverserPosition reverser,
            TrackAtcTravelDirection expectedDirection)
        {
            var context = CreateContext(isFrontCab);
            context.State.frontPosition.frontFacesAtoB = frontFacesAtoB;
            context.State.rearPosition.frontFacesAtoB = frontFacesAtoB;
            context.Input.hasSpeedMeasurement = true;
            context.Input.signedSpeedMps = 0f;
            context.Input.cab.reverserPosition = reverser;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(expectedDirection));
            Assert.That(context.State.isHealthy, Is.True);
        }

        [TestCase("neutral")]
        [TestCase("cab")]
        [TestCase("position")]
        public void ClearsSelectedDirectionForNeutralMissingCabOrUnknownPosition(string condition)
        {
            var context = CreateContext(true);
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.AtoB));

            switch (condition)
            {
                case "neutral": context.Input.cab.reverserPosition = ReverserPosition.Neutral; break;
                case "cab": context.Input.hasCabState = false; break;
                case "position": context.State.isPositionKnown = false; break;
            }

            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentTravelDirection, Is.EqualTo(TrackAtcTravelDirection.Unspecified));
        }

        private static TrainAtcContext CreateContext(bool isFrontCab)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 80f };
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "front", lengthM = 100f });
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "rear", lengthM = 100f });
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph,
                new TrainAtcPosition { atcEdgeId = "front", distanceOnAtcEdgeM = 20f, frontFacesAtoB = true },
                new TrainAtcPosition { atcEdgeId = "rear", distanceOnAtcEdgeM = 80f, frontFacesAtoB = false }), Is.True);
            int carIndex = 7;
            if (isFrontCab)
            {
                carIndex = 0;
            }
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput
            {
                carIndex = carIndex,
                isFrontCab = isFrontCab,
                isKeyInserted = true,
                isInputEnabled = true,
                brakePosition = 8,
                serviceBrakePosition = 7,
                reverserPosition = ReverserPosition.Forward,
                isEmergencyBrake = true
            };
            return context;
        }

        [Test]
        public void InitializationCannotOverwritePositionOrGraph()
        {
            var context = CreateContext(true);
            var graph = context.Graph;
            var front = context.State.frontPosition;
            var rear = context.State.rearPosition;
            Assert.That(TrainAtcLogic.TryInitializePosition(context,
                new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 120f },
                default, default), Is.False);
            Assert.That(context.Graph, Is.SameAs(graph));
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.EqualTo(80f));
            Assert.That(context.State.frontPosition, Is.EqualTo(front));
            Assert.That(context.State.rearPosition, Is.EqualTo(rear));
            Assert.That(context.atcEdgesById.Count, Is.EqualTo(2));
            Assert.That(context.atcEdgesById["front"], Is.SameAs(graph.atcEdge[0]));
        }

        [Test]
        public void InitializationBuildsLookupUsingGraphEdgeReferences()
        {
            var context = CreateContext(true);
            Assert.That(context.atcEdgesById.Count, Is.EqualTo(context.Graph.atcEdge.Count));
            Assert.That(context.atcEdgesById["front"], Is.SameAs(context.Graph.atcEdge[0]));
            Assert.That(context.atcEdgesById["rear"], Is.SameAs(context.Graph.atcEdge[1]));
            Assert.That(context.atcEdgesById.TryGetValue("missing", out _), Is.False);
        }

        [TestCase("nullEdge")]
        [TestCase("emptyId")]
        [TestCase("duplicateId")]
        public void InvalidGraphLeavesLookupEmptyAndCanBeRetried(string condition)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 120f };
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "one", lengthM = 100f });
            switch (condition)
            {
                case "nullEdge": graph.atcEdge.Add(null); break;
                case "emptyId": graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = " ", lengthM = 100f }); break;
                case "duplicateId": graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "one", lengthM = 100f }); break;
            }
            var position = new TrainAtcPosition { atcEdgeId = "one", distanceOnAtcEdgeM = 20f };
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, position, position), Is.False);
            Assert.That(context.atcEdgesById, Is.Empty);
            Assert.That(context.Graph, Is.Null);
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.Zero);
            Assert.That(context.State.isPositionInitialized, Is.False);

            graph.atcEdge.RemoveAt(1);
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, position, position), Is.True);
            Assert.That(context.atcEdgesById["one"], Is.SameAs(graph.atcEdge[0]));
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.EqualTo(120f));
        }

        [Test]
        public void ApplyingVehicleSettingsKeepsMaximumSpeedFromGraph()
        {
            var context = CreateContext(true);
            var settings = new TrainAtcSettings
            {
                maximumOperatingSpeedKmh = 120f,
                maximumSamplingIntervalM = 3f
            };
            context.Settings.CopyFrom(settings);
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.EqualTo(80f));
            Assert.That(context.Settings.maximumSamplingIntervalM, Is.EqualTo(3f));
            Assert.That(context.Graph.maximumOperatingSpeedKmh, Is.EqualTo(80f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidMaximumSpeedDoesNotCommitInitialization(float speed)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = speed };
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "one", lengthM = 100f });
            var position = new TrainAtcPosition { atcEdgeId = "one", distanceOnAtcEdgeM = 20f };
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, position, position), Is.False);
            Assert.That(context.Graph, Is.Null);
            Assert.That(context.Settings.maximumOperatingSpeedKmh, Is.Zero);
            Assert.That(context.atcEdgesById, Is.Empty);
            Assert.That(context.State.isPositionInitialized, Is.False);
        }

        [Test]
        public void CabAndKeyChangesKeepPositionsAndConsistOrientation()
        {
            var context = CreateContext(true);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.hasCurrentPosition, Is.True);
            Assert.That(context.State.currentPosition.atcEdgeId, Is.EqualTo("front"));
            Assert.That(context.State.currentPosition.frontFacesAtoB, Is.True);

            context.Input.cab.isFrontCab = false;
            context.Input.cab.carIndex = 7;
            context.Input.cab.isKeyInserted = false;
            context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentPosition.atcEdgeId, Is.EqualTo("rear"));
            Assert.That(context.State.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(80f));
            Assert.That(context.State.currentPosition.frontFacesAtoB, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);

            context.Input.hasCabState = false;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.hasCurrentPosition, Is.False);
            Assert.That(context.State.currentPosition, Is.EqualTo(default(TrainAtcPosition)));
            context.Input.hasCabState = true;
            context.Input.cab.isFrontCab = true;
            context.Input.cab.carIndex = 0;
            context.Input.cab.isKeyInserted = true;
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(20f));
            Assert.That(context.State.currentPosition.frontFacesAtoB, Is.True);
            Assert.That(context.State.isPositionInitialized, Is.True);
        }

        [Test]
        public void MissingInitialPositionDisablesAtcEvenWithValidCabAndTelegram()
        {
            var context = new TrainAtcContext();
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput { isFrontCab = true, isKeyInserted = true };
            context.Input.frontTelegram = new TrackCircuitAtcTelegram { isValid = true };
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.isHealthy, Is.False);
            Assert.That(context.State.isAtcEnabled, Is.False);
            Assert.That(context.State.hasCurrentPosition, Is.False);
        }

        [TestCase("graph")]
        [TestCase("unknownEdge")]
        [TestCase("distance")]
        [TestCase("duplicateEdge")]
        public void InvalidInitializationDoesNotKeepOneSideOnly(string condition)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "one", lengthM = 100f });
            var front = new TrainAtcPosition { atcEdgeId = "one", distanceOnAtcEdgeM = 20f };
            var rear = front;
            switch (condition)
            {
                case "graph": graph = null; break;
                case "unknownEdge": rear.atcEdgeId = "missing"; break;
                case "distance": rear.distanceOnAtcEdgeM = float.NaN; break;
                case "duplicateEdge": graph.atcEdge.Add(graph.atcEdge[0]); break;
            }
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph, front, rear), Is.False);
            Assert.That(context.State.isPositionInitialized, Is.False);
            Assert.That(context.State.frontPosition.atcEdgeId, Is.Null);
            Assert.That(context.State.rearPosition.atcEdgeId, Is.Null);
            Assert.That(context.Graph, Is.Null);
            Assert.That(context.atcEdgesById, Is.Empty);
        }
    }
}
