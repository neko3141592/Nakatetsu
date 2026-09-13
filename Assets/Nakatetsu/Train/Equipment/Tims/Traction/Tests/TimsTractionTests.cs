using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Notch;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Traction;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class TimsTractionTests
    {
        private static TimsTractionContext Create()
        {
            var c = new TimsTractionContext();
            c.Input.isReady = true;
            c.Input.consistMassKg = 100000f;
            c.Input.powerNotch = 3;
            c.Input.powerStepGain = 0.5f;
            c.Settings.launchAccelerationMps2 = 1f;
            c.Input.units.Add(new TimsTractionUnitInput { isAvailable = true, hasMotorSettings = true,
                motorCount = 4, ratedMotorPowerW = 125000f });
            c.Input.units.Add(c.Input.units[0]);
            return c;
        }

        [TestCase(5f, 50000f, "Const Accel")]
        [TestCase(20f, 25000f, "Const Power")]
        public void ForceTransitionsFromConstantAccelerationToConstantPower(float speed, float expected, string region)
        {
            var c = Create(); c.Input.speedMps = speed;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.constantAccelerationEndSpeedMps, Is.EqualTo(10f));
            Assert.That(c.Output.targetForceN, Is.EqualTo(expected));
            Assert.That(c.Output.targetForcePerVvvfN, Is.EqualTo(expected / 2f));
            Assert.That(c.Output.regionLabel, Is.EqualTo(region));
        }

        [Test]
        public void PressureThresholdCutsTractionUnlessGradientStartIsActive()
        {
            var c = Create(); c.Input.carBCPressuresKPa.Add(5f);
            TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.targetForceN, Is.Zero);
            Assert.That(c.Output.isBCReleaseInterlockActive, Is.True);
            c.Input.isGradientStart = true;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.targetForceN, Is.GreaterThan(0f));
        }

        [Test]
        public void NoCarPressureUsesAggregatePressure()
        {
            var c = Create(); c.Input.currentBCPressureKPa = 6f;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.isBCReleaseInterlockActive, Is.True);
        }

        [Test]
        public void SpeedHoldArmsCapturesSpeedAndCancelsOnAtcBrake()
        {
            var c = Create(); c.Input.manualPowerNotch = 2; c.Input.speedMps = 12f;
            c.State.speedHoldMode = TimsSpeedHoldMode.Arming;
            c.Input.deltaTimeSeconds = 0.5f;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.State.speedHoldMode, Is.EqualTo(TimsSpeedHoldMode.Arming));
            Assert.That(c.Output.targetForceN, Is.Zero);
            TimsTractionLogic.Calculate(c);
            Assert.That(c.State.speedHoldMode, Is.EqualTo(TimsSpeedHoldMode.Active));
            Assert.That(c.State.speedHoldTargetMps, Is.EqualTo(12f));
            c.Input.atcBrakeNotch = 1;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.State.speedHoldMode, Is.EqualTo(TimsSpeedHoldMode.Off));
            Assert.That(c.State.speedHoldTargetMps, Is.Zero);
        }

        [Test]
        public void NegativeTimeDoesNotAdvanceArming()
        {
            var c = Create(); c.Input.manualPowerNotch = 2;
            c.State.speedHoldMode = TimsSpeedHoldMode.Arming;
            c.Input.deltaTimeSeconds = -1f;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.State.speedHoldArmingTimerSeconds, Is.Zero);
        }

        [Test]
        public void BrakeActiveReturnsNoForceCommandAndMissingConfigDispatchesZero()
        {
            var c = Create(); TimsTractionLogic.Calculate(c);
            c.Input.brakeStep = 1; TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.hasForceCommand, Is.False);
            Assert.That(c.Output.targetForceN, Is.Zero);
            c.Input.isReady = false; TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.hasForceCommand, Is.True);
            Assert.That(c.Output.unitTargetForcesN, Is.EqualTo(new[] { 0f, 0f }));
        }

        [Test]
        public void UnavailableUnitsAreExcludedFromPowerAndForceDistribution()
        {
            var c = Create(); c.Input.units[1] = default;
            TimsTractionLogic.Calculate(c);
            Assert.That(c.Output.ratedConsistPowerW, Is.EqualTo(500000f));
            Assert.That(c.Output.activeVvvfCount, Is.EqualTo(1));
            Assert.That(c.Output.unitTargetForcesN[1], Is.Zero);
            Assert.That(c.Output.unitTargetForcesN[0], Is.EqualTo(c.Output.targetForceN));
        }
    }
}
