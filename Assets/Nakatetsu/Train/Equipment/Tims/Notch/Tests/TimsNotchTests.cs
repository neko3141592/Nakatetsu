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
    public sealed class TimsNotchTests
    {
        private static TimsNotchContext Create(bool rearCab = false)
        {
            var c = new TimsNotchContext();
            c.Input.isReady = true;
            c.Settings.brakeSubstepCount = 4;
            c.Input.cars.Add(new TimsCabInput { hasSelection = true,
                selection = rearCab ? TimsCabSelection.Reverse : TimsCabSelection.Forward,
                powerNotch = 3, reverserPosition = TimsReverserPosition.Forward });
            c.Input.cars.Add(new TimsCabInput { hasSelection = true,
                selection = rearCab ? TimsCabSelection.Forward : TimsCabSelection.Reverse,
                powerNotch = 2, reverserPosition = TimsReverserPosition.Forward });
            return c;
        }

        [TestCase(false, 0, 1, 3)]
        [TestCase(true, 1, -1, 2)]
        public void ValidCabSelectsItsCommandsAndFormationDirection(bool rear, int index, int sign, int power)
        {
            var c = Create(rear);
            TimsNotchLogic.Calculate(c);
            Assert.That(c.Output.activatedCabIndex, Is.EqualTo(index));
            Assert.That(c.Output.consistForceSign, Is.EqualTo(sign));
            Assert.That(c.Output.resolvedPowerNotch, Is.EqualTo(power));
            Assert.That(c.Output.isEmergencyBrakeRequested, Is.False);
        }

        [Test]
        public void AtcOverridesManualBrakeAndCutsPower()
        {
            var c = Create();
            var cab = c.Input.cars[0]; cab.brakeNotch = 2; c.Input.cars[0] = cab;
            c.Input.atcBrakeStep = 7;
            TimsNotchLogic.Calculate(c);
            Assert.That(c.Output.manualBrakeStep, Is.EqualTo(5));
            Assert.That(c.Output.resolvedBrakeStep, Is.EqualTo(7));
            Assert.That(c.Output.resolvedPowerNotch, Is.Zero);
            Assert.That(c.Output.resolvedNotchLabel, Is.EqualTo("B2-2"));
        }

        [Test]
        public void InvalidCabRequestsEmergencyAndClearsPreviousCommands()
        {
            var c = Create();
            TimsNotchLogic.Calculate(c);
            c.Input.cars[1] = c.Input.cars[0];
            c.Input.atcBrakeStep = 9;
            TimsNotchLogic.Calculate(c);
            Assert.That(c.Output.isEmergencyBrakeRequested, Is.True);
            Assert.That(c.Output.activatedCabIndex, Is.EqualTo(-1));
            Assert.That(c.Output.manualPowerNotch, Is.Zero);
            Assert.That(c.Output.consistForceSign, Is.Zero);
            Assert.That(c.Output.resolvedBrakeStep, Is.EqualTo(9));
        }

        [Test]
        public void MissingSelectionOrConsistDoesNotReuseOldInput()
        {
            var c = Create();
            c.Input.cars[0] = default;
            TimsNotchLogic.Calculate(c);
            Assert.That(c.Output.isEmergencyBrakeRequested, Is.True);
            c.Input.isReady = false;
            c.Input.atcBrakeStep = 9;
            TimsNotchLogic.Calculate(c);
            Assert.That(c.Output.resolvedBrakeStep, Is.Zero);
        }

        [TestCase(1, 0, 1)]
        [TestCase(2, 3, 8)]
        [TestCase(7, 0, 25)]
        public void NotchConversionRoundTrips(int notch, int substep, int expected)
        {
            TimsNotchCalculator.ToContinuousBrakeNotch(notch, substep, 4, out int value);
            Assert.That(value, Is.EqualTo(expected));
            TimsNotchCalculator.ToSubStepBrakeNotch(value, 4, out int restored, out int fraction);
            Assert.That(restored, Is.EqualTo(notch));
            Assert.That(fraction, Is.EqualTo(substep));
        }
    }
}
