using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Train.Brake.ControlDevice;
using Nakatetsu.Train.Brake.Cylinder;
using Nakatetsu.Train.Equipment;
using Nakatetsu.Train.Tims.Communication;
using Nakatetsu.Train.Tims.Notch;
using Nakatetsu.Train.Tims.Brake;
using Nakatetsu.Train.Tims.Traction;

namespace Nakatetsu.Train.Tims.Tests
{
    public sealed class TimsBrakeTests
    {
        private static TimsBrakeContext Create()
        {
            var c = new TimsBrakeContext();
            Configure(c);
            return c;
        }

        private static void Configure(TimsBrakeContext c)
        {
            c.Input.canReleaseEmergencyBrake = true;
            c.Input.brakeStep = 1;
            c.Settings.brakeTargetDecelerationsMps2.Add(1f);
            c.Settings.minimumServiceBrakePressureKPa = 0f;
            c.Input.cars.Add(new TimsBrakeCarInput { massKg = 40000f, isVvvfMotorCar = true,
                airCapN = 100000f, airForcePerKPa = 100f, maxBCPressureKPa = 300f });
            c.Input.cars.Add(new TimsBrakeCarInput { massKg = 20000f, isTrailerCar = true,
                airCapN = 100000f, airForcePerKPa = 100f, maxBCPressureKPa = 300f });
        }

        [Test]
        public void RegenRequestUsesMotorMassAndActualRegenReducesTrailerAir()
        {
            var c = Create();
            c.Input.cars[0].regenForceN = 50000f;
            TimsBrakeLogic.Calculate(c);
            Assert.That(c.Output.targetTotalBrakeForceN, Is.EqualTo(60000f));
            Assert.That(c.Output.carCommands[0].targetRegenForceN, Is.EqualTo(60000f));
            Assert.That(c.Output.carCommands[1].targetRegenForceN, Is.Zero);
            Assert.That(c.Output.carCommands[0].targetAirForceN, Is.Zero);
            Assert.That(c.Output.carCommands[1].targetAirForceN, Is.EqualTo(10000f).Within(0.01f));
        }

        [Test]
        public void MinimumPressureScalesWithLoadAndClampsToMaximumPressure()
        {
            var c = Create();
            c.Settings.minimumServiceBrakePressureKPa = 40f;
            c.Input.cars[0].maxBCPressureKPa = 60f;
            TimsBrakeLogic.Calculate(c);
            Assert.That(c.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 6000f, 4000f }));
            Assert.That(c.Output.carCommands[0].targetRegenForceN, Is.EqualTo(50000f));
        }

        [Test]
        public void BrakeReleaseClearsPressureAndCommands()
        {
            var c = Create();
            TimsBrakeLogic.Calculate(c);
            c.Input.brakeStep = 0;
            TimsBrakeLogic.Calculate(c);
            foreach (var command in c.Output.carCommands)
            {
                Assert.That(command.targetAirForceN, Is.Zero);
                Assert.That(command.targetRegenForceN, Is.Zero);
            }
        }

        [Test]
        public void CalculatedAirBrakeForcesArePublishedToMasterBus()
        {
            var timsObject = new GameObject("TIMS");
            try
            {
                TimsCommunicationController communicationController =
                    timsObject.AddComponent<TimsCommunicationController>();
                TimsBrakeController brakeController =
                    timsObject.AddComponent<TimsBrakeController>();
                brakeController.Configure(communicationController);
                Configure(brakeController.Context);

                Assert.That(brakeController.CalculateAndPublish(), Is.True);
                Assert.That(brakeController.TryGetTargetAirBrakeForceN(
                    0,
                    out float firstCarForceN), Is.True);
                Assert.That(brakeController.TryGetTargetAirBrakeForceN(
                    1,
                    out float secondCarForceN), Is.True);
                Assert.That(firstCarForceN, Is.EqualTo(40000f));
                Assert.That(secondCarForceN, Is.EqualTo(20000f));
            }
            finally
            {
                Object.DestroyImmediate(timsObject);
            }
        }

        [Test]
        public void MissingCommandsRemovePreviousBrakeForceFromMasterBus()
        {
            var timsObject = new GameObject("TIMS");
            try
            {
                TimsCommunicationController communicationController =
                    timsObject.AddComponent<TimsCommunicationController>();
                TimsBrakeController brakeController =
                    timsObject.AddComponent<TimsBrakeController>();
                brakeController.Configure(communicationController);
                Configure(brakeController.Context);
                brakeController.CalculateAndPublish();

                brakeController.Context.Input.canReleaseEmergencyBrake = false;
                brakeController.CalculateAndPublish();

                Assert.That(brakeController.TryGetTargetAirBrakeForceN(0, out _), Is.False);
                Assert.That(communicationController.MasterBus.TryGetBool(
                    TimsBrakeController.IsEmergencyKey,
                    out bool isEmergency) && isEmergency, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(timsObject);
            }
        }

        [Test]
        public void AdapterReadsAssignedCarsBrakeForceFromMasterBus()
        {
            var communicationObject = new GameObject("TIMS");
            var brakeObject = new GameObject("Brake Control Device");
            try
            {
                TimsCommunicationController communicationController =
                    communicationObject.AddComponent<TimsCommunicationController>();
                TimsBrakeController timsBrakeController =
                    communicationObject.AddComponent<TimsBrakeController>();
                timsBrakeController.Configure(communicationController);
                Configure(timsBrakeController.Context);
                timsBrakeController.Context.Input.cars[0].massKg = 9000f;
                timsBrakeController.Context.Input.cars[1].massKg = 4500f;
                timsBrakeController.CalculateAndPublish();
                TrainEquipmentAssignment assignment =
                    brakeObject.AddComponent<TrainEquipmentAssignment>();
                assignment.AssignCarIndex(1);
                BrakeControlDevice brakeControlDevice =
                    brakeObject.AddComponent<BrakeControlDevice>();
                var cylinderObject = new GameObject("Brake Cylinder");
                cylinderObject.transform.SetParent(brakeObject.transform, false);
                cylinderObject.AddComponent<BrakeCylinder>();
                brakeControlDevice.RefreshBrakeCylinders();

                BrakeControlDeviceTimsAdapter adapter =
                    brakeObject.AddComponent<BrakeControlDeviceTimsAdapter>();
                adapter.Configure(timsBrakeController);

                Assert.That(adapter.ReadAndApply(2f), Is.True);
                Assert.That(brakeControlDevice.TargetBrakeForceN, Is.EqualTo(4500f));
                Assert.That(brakeControlDevice.ActualBrakeForceN, Is.EqualTo(4500f));
            }
            finally
            {
                Object.DestroyImmediate(communicationObject);
                Object.DestroyImmediate(brakeObject);
            }
        }

        [Test]
        public void MissingDependenciesPublishEmergencyWithoutNewCommands()
        {
            var c = Create();
            TimsBrakeLogic.Calculate(c);
            c.Input.canReleaseEmergencyBrake = false;
            TimsBrakeLogic.Calculate(c);
            Assert.That(c.Output.isEmergency, Is.True);
            Assert.That(c.Output.hasCommands, Is.False);
        }

        [Test]
        public void ShrinkingConsistRemovesCommandsForRemovedCars()
        {
            var c = Create();
            TimsBrakeLogic.Calculate(c);
            c.Input.cars.RemoveAt(1);
            TimsBrakeLogic.Calculate(c);
            Assert.That(c.Output.carCommands.Count, Is.EqualTo(1));
            Assert.That(c.Workspace.minimumAirForcesN.Count, Is.EqualTo(1));
            Assert.That(c.Output.totalMassKg, Is.EqualTo(40000f));
        }

        [Test]
        public void LegacyZeroAdditionalAirCapacityMeansUnboundedFallback()
        {
            var c = Create();
            c.Input.cars[0].airCapN = 0f;
            TimsBrakeLogic.Calculate(c);
            Assert.That(c.Output.carCommands[0].targetAirForceN, Is.EqualTo(40000f).Within(0.01f));
        }

        [Test]
        public void SaturatedAllocationRedistributesRemainingForce()
        {
            var result = TimsBrakeCalculator.AllocateEvenlyWithSaturation(new[] { 10f, 100f, 0f }, 80f);
            Assert.That(result, Is.EqualTo(new[] { 10f, 70f, 0f }).Within(0.001f));
        }

        [TestCase(0, 0f)]
        [TestCase(1, 1f)]
        [TestCase(3, 1.5f)]
        [TestCase(5, 2f)]
        public void BrakeTableInterpolatesAndHandlesReleaseAndLastStep(int step, float expected)
        {
            var table = new List<float> { 1f, 2f };
            Assert.That(TimsBrakeCalculator.TryGetDeceleration(step, 4, table, out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void NearestStepPrefersStrongerBrakeOnTieAndRejectsInvalidInput()
        {
            var table = new List<float> { 1f, 2f };
            Assert.That(TimsBrakeCalculator.TryGetNearestBrakeStep(1.125f, 4, table, out int step), Is.True);
            Assert.That(step, Is.EqualTo(2));
            Assert.That(TimsBrakeCalculator.TryGetDeceleration(6, 4, table, out _), Is.False);
            Assert.That(TimsBrakeCalculator.TryGetDeceleration(1, 0, table, out _), Is.False);
        }
    }
}
