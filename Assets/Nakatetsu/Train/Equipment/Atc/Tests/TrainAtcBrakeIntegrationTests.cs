using System.Linq;
using System;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcBrakeIntegrationTests
    {
        [Test]
        public void UsesProvisionalDecelerationDefaults()
        {
            var settings = new TrainAtcSettings();
            Assert.That(settings.serviceDecelerationMps2, Is.EqualTo(0.5f));
            Assert.That(settings.emergencyDecelerationMps2, Is.EqualTo(1.2f));
            Assert.That(settings.maximumDownhillGradientPermille, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(10f)]
        [TestCase(-10f)]
        public void IntegratesConstantGradientToStopTarget(float gradientPermille)
        {
            var context = CreateContext(100f, gradientPermille, out _);
            TrainAtcLogic.Calculate(context);

            var pattern = context.State.brakePattern;
            Assert.That(pattern.normalPattern.Count, Is.EqualTo(21));
            double serviceDeceleration = 0.5d + 9.80665d * gradientPermille / 1000d;
            double emergencyDeceleration = 1.2d + 9.80665d * gradientPermille / 1000d;
            for (int i = 0; i < 21; i++)
            {
                double remainingDistanceM = Math.Max(0d, 95d - i * 5d);
                Assert.That(pattern.normalPattern[i].speedLimitMps,
                    Is.EqualTo(Math.Sqrt(2d * serviceDeceleration * remainingDistanceM)).Within(0.0002d));
                Assert.That(pattern.emergencyPattern[i].speedLimitMps,
                    Is.EqualTo(Math.Sqrt(2d * emergencyDeceleration * remainingDistanceM)).Within(0.0002d));
            }
        }

        [TestCase(1f, 1f, 10f)]
        [TestCase(1f, 3f, 5f)]
        [TestCase(3f, 1f, 15f)]
        public void WeightsGradientForcesByEachCarMass(float frontMass, float rearMass, float expectedGradient)
        {
            var context = CreateContext(100f, 0f, out _);
            var edge = context.atcEdgesById["path"];
            edge.gradientProfiles.Clear();
            AddGradient(edge, 0f, 0f);
            AddGradient(edge, 60f, 0f);
            AddGradient(edge, 65f, 20f);
            AddGradient(edge, 100f, 20f);
            context.Input.cars.Clear();
            context.Input.cars.Add(new TrainAtcCarInput { massKg = frontMass * 1000f, centerDistanceFromFrontM = 0f });
            context.Input.cars.Add(new TrainAtcCarInput { massKg = rearMass * 1000f, centerDistanceFromFrontM = 20f });

            TrainAtcLogic.Calculate(context);

            // 75mと80mでは先頭車が20‰、後方車が0‰に載る。
            AssertSegmentDeceleration(context, 15, 0.5d + 9.80665d * expectedGradient / 1000d,
                1.2d + 9.80665d * expectedGradient / 1000d);
        }

        [Test]
        public void UsesFutureCarPositionsForEachSample()
        {
            var context = CreateContext(100f, 0f, out _);
            AddLinearGradient(context.atcEdgesById["path"], 0f, 20f);
            TrainAtcLogic.Calculate(context);

            // 50mの勾配は10‰。現在位置の勾配を経路全体に流用しない。
            AssertSegmentDeceleration(context, 10, 0.5d + 9.80665d * 10d / 1000d,
                1.2d + 9.80665d * 10d / 1000d);
        }

        [Test]
        public void UsesLowerGradientAtBothSegmentEnds()
        {
            var context = CreateContext(100f, 0f, out _);
            AddLinearGradient(context.atcEdgesById["path"], 20f, 0f);
            TrainAtcLogic.Calculate(context);

            // 50mは10‰、55mは9‰。減速度が小さい55m側で積分する。
            AssertSegmentDeceleration(context, 10, 0.5d + 9.80665d * 9d / 1000d,
                1.2d + 9.80665d * 9d / 1000d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IncludesGradientValleyBetweenBrakeSamples(bool reverse)
        {
            var context = CreateContext(100f, 0f, out var route);
            var edge = context.atcEdgesById["path"];
            edge.gradientProfiles.Clear();
            AddGradient(edge, 0f, 0f);
            if (reverse)
            {
                AddGradient(edge, 45f, 0f);
                AddGradient(edge, 47.5f, 20f);
                AddGradient(edge, 50f, 0f);
                context.Input.cab.reverserPosition = ReverserPosition.Reverse;
            }
            else
            {
                AddGradient(edge, 50f, 0f);
                AddGradient(edge, 52.5f, -20f);
                AddGradient(edge, 55f, 0f);
            }
            AddGradient(edge, 100f, 0f);
            SetTelegram(context, route);
            TrainAtcLogic.Calculate(context);

            // 50mと55mが平坦でも、間の下り20‰を取りこぼさない。
            AssertSegmentDeceleration(context, 10, 0.5d - 9.80665d * 20d / 1000d,
                1.2d - 9.80665d * 20d / 1000d);
        }

        [Test]
        public void IncludesDownhillAtEdgeExitBeforeGradientJumps()
        {
            var context = CreateContext(52.5f, 0f, out var route);
            var first = context.atcEdgesById["path"];
            first.gradientProfiles.Clear();
            AddGradient(first, 0f, 0f);
            AddGradient(first, 50f, 0f);
            AddGradient(first, 52.5f, -20f);
            var next = CreateEdge("next", "n1", "n2", 47.5f, 0f);
            context.Graph.atcEdge.Add(next);
            context.atcEdgesById.Add(next.atcEdgeId, next);
            route.atcEdgePath.Add(next.atcEdgeId);
            route.stopAtcEdgeId = next.atcEdgeId;
            TrainAtcLogic.Calculate(context);

            // 52.5m境界の直前は下り20‰、直後は平坦。両側の勾配を確認する。
            AssertSegmentDeceleration(context, 10, 0.5d - 9.80665d * 20d / 1000d,
                1.2d - 9.80665d * 20d / 1000d);
        }

        [TestCase(0f)]
        [TestCase(20f)]
        [TestCase(40f)]
        public void UnknownRearUsesLeadingCarForServiceAndConfiguredDownhillForEmergency(float maximumDownhill)
        {
            var context = CreateContext(100f, 10f, out _);
            context.Settings.maximumDownhillGradientPermille = maximumDownhill;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 20f });
            var other = CreateEdge("other", "x0", "x1", 30f, -90f);
            context.Graph.atcEdge.Add(other);
            context.atcEdgesById.Add(other.atcEdgeId, other);

            TrainAtcLogic.Calculate(context);

            // 経路外の勾配は設定値で補う。Graph内に90‰があっても自動取得しない。
            AssertSegmentDeceleration(context, 0, 0.5d + 9.80665d * 10d / 1000d,
                1.2d + 9.80665d * (10d - maximumDownhill) / 2000d);
        }

        [Test]
        public void KnownRearGradientDoesNotUseEmergencyFallback()
        {
            var context = CreateContext(100f, 10f, out _);
            context.Settings.maximumDownhillGradientPermille = 40f;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 20f });
            var other = CreateEdge("other", "x0", "x1", 30f, -90f);
            context.Graph.atcEdge.Add(other);
            context.atcEdgesById.Add(other.atcEdgeId, other);
            TrainAtcLogic.Calculate(context);

            AssertSegmentDeceleration(context, 5, 0.5d + 9.80665d * 10d / 1000d,
                1.2d + 9.80665d * 10d / 1000d);
        }

        [Test]
        public void CopyingDownhillSettingChangesOnlyEmergencyCurveBeforeRearEntersPath()
        {
            var context = CreateContext(100f, 10f, out _);
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 20f });
            TrainAtcLogic.Calculate(context);
            var previousNormal = context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps).ToArray();
            var previousEmergency = context.State.brakePattern.emergencyPattern.Select(sample => sample.speedLimitMps).ToArray();

            context.Settings.CopyFrom(new TrainAtcSettings { maximumDownhillGradientPermille = 40f });
            TrainAtcLogic.Calculate(context);

            Assert.That(context.Settings.maximumDownhillGradientPermille, Is.EqualTo(40f));
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.EqualTo(previousNormal).Within(0.0001f));
            Assert.That(context.State.brakePattern.emergencyPattern[0].speedLimitMps, Is.LessThan(previousEmergency[0]));
            Assert.That(context.State.brakePattern.emergencyPattern[5].speedLimitMps,
                Is.EqualTo(previousEmergency[5]).Within(0.0001f));
        }

        [Test]
        public void IgnoresInvalidGradientProfilesOutsideTelegramPath()
        {
            var context = CreateContext(100f, 10f, out _);
            context.Settings.maximumDownhillGradientPermille = 40f;
            var other = CreateEdge("other", "x0", "x1", 30f, -90f);
            other.gradientProfiles = null;
            context.Graph.atcEdge.Add(other);
            context.atcEdgesById.Add(other.atcEdgeId, other);
            TrainAtcLogic.Calculate(context);

            AssertSegmentDeceleration(context, 0, 0.5d + 9.80665d * 10d / 1000d,
                1.2d + 9.80665d * 10d / 1000d);
        }

        [TestCase(true, ReverserPosition.Forward, 10f)]
        [TestCase(true, ReverserPosition.Reverse, -10f)]
        [TestCase(false, ReverserPosition.Forward, -10f)]
        [TestCase(false, ReverserPosition.Reverse, 10f)]
        public void ResolvesGradientSignFromCabAndReverser(bool isFrontCab, ReverserPosition reverser, float gradient)
        {
            var context = CreateContext(100f, 10f, out var route);
            context.Input.cab.isFrontCab = isFrontCab;
            context.Input.cab.reverserPosition = reverser;
            SetTelegram(context, route);
            TrainAtcLogic.Calculate(context);

            AssertSegmentDeceleration(context, 10, 0.5d + 9.80665d * gradient / 1000d,
                1.2d + 9.80665d * gradient / 1000d);
        }

        [Test]
        public void IncludesFrontReceiverOffsetInFutureCarPosition()
        {
            var context = CreateContext(100f, 0f, out _);
            AddLinearGradient(context.atcEdgesById["path"], 0f, 20f);
            context.Input.cars[0] = new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 10f };
            context.Input.frontReceiverDistanceFromFrontM = 30f;
            TrainAtcLogic.Calculate(context);

            // 受信機40mに対して車両中心60m。勾配は12‰。
            AssertSegmentDeceleration(context, 8, 0.5d + 9.80665d * 12d / 1000d,
                1.2d + 9.80665d * 12d / 1000d);
        }

        [Test]
        public void IncludesRearReceiverOffsetWhenTravellingToConsistRear()
        {
            var context = CreateContext(100f, 0f, out var route);
            AddLinearGradient(context.atcEdgesById["path"], 0f, 20f);
            context.Input.cars[0] = new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 10f };
            context.Input.rearReceiverDistanceFromFrontM = 30f;
            context.Input.cab.isFrontCab = false;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            SetTelegram(context, route);
            TrainAtcLogic.Calculate(context);

            // 経路60mに対して車両中心40m、A基準では60m。進行方向へは下り12‰。
            AssertSegmentDeceleration(context, 12, 0.5d - 9.80665d * 12d / 1000d,
                1.2d - 9.80665d * 12d / 1000d);
        }

        [Test]
        public void OutOfPathLeadingCarUsesEndpointForServiceAndWorstDownhillForEmergency()
        {
            var context = CreateContext(100f, 0f, out _);
            AddLinearGradient(context.atcEdgesById["path"], 0f, 20f);
            context.Input.frontReceiverDistanceFromFrontM = 20f;
            context.Settings.maximumDownhillGradientPermille = 20f;
            TrainAtcLogic.Calculate(context);

            AssertSegmentDeceleration(context, 18, 0.5d + 9.80665d * 20d / 1000d,
                1.2d - 9.80665d * 20d / 1000d);
        }

        [Test]
        public void AcceptsReceiverMountedAheadOfConsistFront()
        {
            var context = CreateContext(100f, 0f, out _);
            context.Input.frontReceiverDistanceFromFrontM = -5f;
            TrainAtcLogic.Calculate(context);

            AssertSegmentDeceleration(context, 10, 0.5d, 1.2d);
        }

        [Test]
        public void RearwardTravelUsesFixedRearCarAsLeadingGradientFallback()
        {
            var context = CreateContext(100f, 0f, out var route);
            AddLinearGradient(context.atcEdgesById["path"], 0f, 20f);
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 20f });
            context.Input.rearReceiverDistanceFromFrontM = 10f;
            context.Settings.maximumDownhillGradientPermille = 20f;
            context.Input.cab.isFrontCab = false;
            context.Input.cab.reverserPosition = ReverserPosition.Forward;
            SetTelegram(context, route);
            TrainAtcLogic.Calculate(context);

            // 経路0mの先頭車中心は10mにあり下り18‰。後方の固定前側車両は経路外。
            AssertSegmentDeceleration(context, 0, 0.5d - 9.80665d * 18d / 1000d,
                1.2d - 9.80665d * 19d / 1000d);
        }

        [Test]
        public void ReadsFollowingEdgeGradientInItsTravelDirection()
        {
            var context = CreateContext(50f, 0f, out var route);
            var next = CreateEdge("next", "n2", "n1", 50f, 20f);
            context.Graph.atcEdge.Add(next);
            context.atcEdgesById.Add(next.atcEdgeId, next);
            route.atcEdgePath.Add(next.atcEdgeId);
            route.stopAtcEdgeId = next.atcEdgeId;
            TrainAtcLogic.Calculate(context);

            Assert.That(context.State.brakePattern.pathLengthM, Is.EqualTo(100f));
            AssertSegmentDeceleration(context, 10, 0.5d - 9.80665d * 20d / 1000d,
                1.2d - 9.80665d * 20d / 1000d);
        }

        [Test]
        public void IntegrationKeepsPermanentAndEmergencySpeedCaps()
        {
            var context = CreateContext(500f, 0f, out _);
            context.atcEdgesById["path"].speedLimitSections.Add(new TrackAtcSpeedLimitSection
                { startDistanceOnAtcEdgeM = 0f, endDistanceOnAtcEdgeM = 100f, speedLimitKmh = 18f });
            TrainAtcLogic.Calculate(context);

            Assert.That(context.State.brakePattern.normalPattern[0].speedLimitMps, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(context.State.brakePattern.emergencyPattern[0].speedLimitMps,
                Is.EqualTo(28f / 3.6f).Within(0.0001f));
            Assert.That(context.State.brakePattern.normalPattern[99].speedLimitMps, Is.Zero);
            Assert.That(context.State.brakePattern.emergencyPattern[99].speedLimitMps, Is.Zero);
        }

        [Test]
        public void IntegrationPropagatesOrpCapAndEmergencyStopSeed()
        {
            var context = CreateContext(200f, 0f, out var route);
            route.overrunProtectionMode = OverrunProtectionMode.Restricted;
            TrainAtcLogic.Calculate(context);
            var pattern = context.State.brakePattern;

            Assert.That(pattern.normalPattern[0].speedLimitMps,
                Is.EqualTo(Math.Sqrt(Math.Pow(25d / 3.6d, 2d) + 100d)).Within(0.0002d));
            Assert.That(pattern.normalPattern[20].speedLimitMps, Is.EqualTo(25f / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[20].speedLimitMps, Is.EqualTo(35f / 3.6f).Within(0.0001f));
            Assert.That(pattern.emergencyPattern[38].speedLimitMps, Is.EqualTo(Math.Sqrt(12d)).Within(0.0002d));
            Assert.That(pattern.emergencyPattern[39].speedLimitMps, Is.Zero);
            Assert.That(pattern.emergencyPattern[40].speedLimitMps, Is.Zero);
        }

        [Test]
        public void IntegrationPropagatesUnprotectedServiceStopMargin()
        {
            var context = CreateContext(200f, 0f, out var route);
            route.overrunProtectionMode = OverrunProtectionMode.None;
            TrainAtcLogic.Calculate(context);

            Assert.That(context.State.brakePattern.normalPattern[0].speedLimitMps, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(context.State.brakePattern.normalPattern[20].speedLimitMps, Is.Zero);
            Assert.That(context.State.brakePattern.emergencyPattern[0].speedLimitMps,
                Is.EqualTo(Math.Sqrt(2d * 1.2d * 195d)).Within(0.0002d));
        }

        [TestCase("missingMasses")]
        [TestCase("emptyCars")]
        [TestCase("zeroMass")]
        [TestCase("negativeMass")]
        [TestCase("nanMass")]
        [TestCase("infiniteMass")]
        [TestCase("negativeCenter")]
        [TestCase("nanCenter")]
        [TestCase("infiniteCenter")]
        [TestCase("duplicateCenters")]
        [TestCase("unorderedCenters")]
        [TestCase("nanFrontOffset")]
        [TestCase("infiniteFrontOffset")]
        [TestCase("zeroServiceDeceleration")]
        [TestCase("negativeServiceDeceleration")]
        [TestCase("nanServiceDeceleration")]
        [TestCase("infiniteServiceDeceleration")]
        [TestCase("zeroEmergencyDeceleration")]
        [TestCase("negativeEmergencyDeceleration")]
        [TestCase("nanEmergencyDeceleration")]
        [TestCase("infiniteEmergencyDeceleration")]
        [TestCase("negativeMaximumDownhill")]
        [TestCase("nanMaximumDownhill")]
        [TestCase("infiniteMaximumDownhill")]
        [TestCase("missingGradient")]
        [TestCase("nanGradient")]
        [TestCase("missingGradientEndpoint")]
        [TestCase("nonpositiveEffectiveDeceleration")]
        public void InvalidIntegrationInputClearsPreviousPattern(string condition)
        {
            var context = CreateContext(100f, 0f, out _);
            TrainAtcLogic.Calculate(context);
            Assert.That(context.State.brakePattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Not.Empty);
            var car = context.Input.cars[0];
            switch (condition)
            {
                case "missingMasses": context.Input.hasCarMasses = false; break;
                case "emptyCars": context.Input.cars.Clear(); break;
                case "zeroMass": car.massKg = 0f; break;
                case "negativeMass": car.massKg = -1f; break;
                case "nanMass": car.massKg = float.NaN; break;
                case "infiniteMass": car.massKg = float.PositiveInfinity; break;
                case "negativeCenter": car.centerDistanceFromFrontM = -1f; break;
                case "nanCenter": car.centerDistanceFromFrontM = float.NaN; break;
                case "infiniteCenter": car.centerDistanceFromFrontM = float.PositiveInfinity; break;
                case "duplicateCenters": context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 0f }); break;
                case "unorderedCenters": car.centerDistanceFromFrontM = 30f; context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 20f }); break;
                case "nanFrontOffset": context.Input.frontReceiverDistanceFromFrontM = float.NaN; break;
                case "infiniteFrontOffset": context.Input.frontReceiverDistanceFromFrontM = float.PositiveInfinity; break;
                case "zeroServiceDeceleration": context.Settings.serviceDecelerationMps2 = 0f; break;
                case "negativeServiceDeceleration": context.Settings.serviceDecelerationMps2 = -1f; break;
                case "nanServiceDeceleration": context.Settings.serviceDecelerationMps2 = float.NaN; break;
                case "infiniteServiceDeceleration": context.Settings.serviceDecelerationMps2 = float.PositiveInfinity; break;
                case "zeroEmergencyDeceleration": context.Settings.emergencyDecelerationMps2 = 0f; break;
                case "negativeEmergencyDeceleration": context.Settings.emergencyDecelerationMps2 = -1f; break;
                case "nanEmergencyDeceleration": context.Settings.emergencyDecelerationMps2 = float.NaN; break;
                case "infiniteEmergencyDeceleration": context.Settings.emergencyDecelerationMps2 = float.PositiveInfinity; break;
                case "negativeMaximumDownhill": context.Settings.maximumDownhillGradientPermille = -1f; break;
                case "nanMaximumDownhill": context.Settings.maximumDownhillGradientPermille = float.NaN; break;
                case "infiniteMaximumDownhill": context.Settings.maximumDownhillGradientPermille = float.PositiveInfinity; break;
                case "missingGradient": context.atcEdgesById["path"].gradientProfiles.Clear(); break;
                case "nanGradient": context.atcEdgesById["path"].gradientProfiles[0].gradientPermille = float.NaN; break;
                case "missingGradientEndpoint": context.atcEdgesById["path"].gradientProfiles[1].distanceOnAtcEdgeM = 99f; break;
                case "nonpositiveEffectiveDeceleration": AddLinearGradient(context.atcEdgesById["path"], -60f, -60f); break;
            }
            if (context.Input.cars.Count > 0) context.Input.cars[0] = car;
            TrainAtcLogic.Calculate(context);

            var pattern = context.State.brakePattern;
            Assert.That(pattern.pathLengthM, Is.Zero);
            Assert.That(pattern.samplingIntervalM, Is.Zero);
            Assert.That(pattern.pathAtcEdges, Is.Empty);
            Assert.That(pattern.normalPattern.Select(sample => sample.speedLimitMps), Is.Empty);
            Assert.That(pattern.emergencyPattern.Select(sample => sample.speedLimitMps), Is.Empty);
        }

        private static TrainAtcContext CreateContext(float lengthM, float gradientPermille,
            out TrackCircuitAtcRouteInfomation route)
        {
            var context = new TrainAtcContext();
            var graph = new TrackAtcGraphDefinition { maximumOperatingSpeedKmh = 180f };
            graph.atcEdge.Add(CreateEdge("path", "n0", "n1", lengthM, gradientPermille));
            Assert.That(TrainAtcLogic.TryInitializePosition(context, graph,
                new TrainAtcPosition { atcEdgeId = "path", distanceOnAtcEdgeM = lengthM / 2f, frontFacesAtoB = true },
                new TrainAtcPosition { atcEdgeId = "path", distanceOnAtcEdgeM = lengthM / 2f, frontFacesAtoB = true }), Is.True);
            context.Input.hasCabState = true;
            context.Input.cab = new TrainAtcCabInput
            {
                isFrontCab = true,
                isKeyInserted = true,
                isInputEnabled = true,
                reverserPosition = ReverserPosition.Forward,
                isNeutral = true
            };
            context.Input.hasSpeedMeasurement = true;
            context.Input.hasBrakeSettings = true;
            context.Input.brakeSettings.brakeSubstepCount = 1;
            context.Input.brakeSettings.maximumServiceBrakeStep = 7;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.AddRange(
                new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f });
            context.Input.hasCarMasses = true;
            context.Input.cars.Add(new TrainAtcCarInput { massKg = 1000f, centerDistanceFromFrontM = 0f });
            route = new TrackCircuitAtcRouteInfomation
                { stopAtcEdgeId = "path", overrunProtectionMode = OverrunProtectionMode.Normal };
            route.atcEdgePath.Add("path");
            SetTelegram(context, route);
            return context;
        }

        private static TrackAtcGraphEdge CreateEdge(string id, string nodeA, string nodeB, float lengthM, float gradient)
        {
            var edge = new TrackAtcGraphEdge
                { atcEdgeId = id, lengthM = lengthM, atcNodeAId = nodeA, atcNodeBId = nodeB };
            AddLinearGradient(edge, gradient, gradient);
            return edge;
        }

        private static void AddLinearGradient(TrackAtcGraphEdge edge, float startGradient, float endGradient)
        {
            edge.gradientProfiles.Clear();
            AddGradient(edge, 0f, startGradient);
            AddGradient(edge, edge.lengthM, endGradient);
        }

        private static void AddGradient(TrackAtcGraphEdge edge, float distanceM, float gradient)
        {
            edge.gradientProfiles.Add(new TrackAtcGradientProfile
                { distanceOnAtcEdgeM = distanceM, gradientPermille = gradient });
        }

        private static void SetTelegram(TrainAtcContext context, TrackCircuitAtcRouteInfomation route)
        {
            // 運転台・レバーサを切り替えたケースでも、その方向の電文を供給する。
            var telegram = new TrackCircuitAtcTelegram { isValid = true };
            telegram.atcRouteInfomation.Add(("path", TrackAtcTravelDirection.AtoB), route);
            telegram.atcRouteInfomation.Add(("path", TrackAtcTravelDirection.BtoA), route);
            context.Input.frontTelegram = telegram;
            context.Input.rearTelegram = telegram;
        }

        private static void AssertSegmentDeceleration(TrainAtcContext context, int index,
            double serviceDeceleration, double emergencyDeceleration)
        {
            var pattern = context.State.brakePattern;
            Assert.That(pattern.normalPattern.Count, Is.GreaterThan(index + 1));
            double serviceSquaredDifference = Math.Pow(pattern.normalPattern[index].speedLimitMps, 2d) -
                Math.Pow(pattern.normalPattern[index + 1].speedLimitMps, 2d);
            double emergencySquaredDifference = Math.Pow(pattern.emergencyPattern[index].speedLimitMps, 2d) -
                Math.Pow(pattern.emergencyPattern[index + 1].speedLimitMps, 2d);
            Assert.That(serviceSquaredDifference, Is.EqualTo(2d * serviceDeceleration * 5d).Within(0.0005d));
            Assert.That(emergencySquaredDifference, Is.EqualTo(2d * emergencyDeceleration * 5d).Within(0.0005d));
        }
    }
}
