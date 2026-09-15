using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using NUnit.Framework;

namespace Nakatetsu.Train.Tests
{
    public sealed class EbDeviceLogicTests
    {
        [Test]
        public void RequestsEmergencyBrakeAfterConfiguredInactivity()
        {
            EbDeviceContext context = CreateContext(10f);

            EbDeviceLogic.Calculate(context, 4f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);

            EbDeviceLogic.Calculate(context, 6f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(10f));
        }

        [Test]
        public void MasterControllerOperationResetsInactivity()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 8f);

            context.Input.masterController.powerPosition = 1;
            EbDeviceLogic.Calculate(context, 3f);

            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(10f));
        }

        [Test]
        public void DisabledMasterControllerStopsMonitoring()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 10f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);

            context.Input.masterController.isInputEnabled = false;
            EbDeviceLogic.Calculate(context, 1f);

            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
        }

        [Test]
        public void InactiveCabResetsAndDoesNotAccumulateTime()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 10f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);

            context.Input.masterController.isActiveCab = false;
            EbDeviceLogic.Calculate(context, 100f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(10f));

            context.Input.masterController.isActiveCab = true;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(1f));
        }

        [TestCase(0f, false)]
        [TestCase(4.999f, false)]
        [TestCase(-4.999f, false)]
        [TestCase(5f, true)]
        [TestCase(-5f, true)]
        [TestCase(30f, true)]
        [TestCase(-30f, true)]
        public void MonitorsOnlyAtOrAboveAbsoluteFiveKmh(float speedKmh, bool isRunning)
        {
            var context = CreateContext(60f);
            context.Input.masterController.speedMps = speedKmh / 3.6f;
            EbDeviceLogic.Calculate(context, 60f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.EqualTo(isRunning));
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(isRunning ? 60f : 0f));
        }

        [Test]
        public void SlowingBelowThresholdClearsRequestAndRestartCountsFromZero()
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 60f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);

            context.Input.masterController.speedMps = 4f / 3.6f;
            EbDeviceLogic.Calculate(context, 120f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(60f));

            context.Input.masterController.speedMps = -5f / 3.6f;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(1f));
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
        }

        [Test]
        public void SpeedChangeAloneDoesNotCountAsMasterControllerOperation()
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 59f);
            context.Input.masterController.speedMps = 20f;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidSpeedClearsMonitoring(float speedMps)
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 60f);
            context.Input.masterController.speedMps = speedMps;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
        }

        private static EbDeviceContext CreateContext(float activationDelaySeconds)
        {
            var context = new EbDeviceContext();
            context.Settings.activationDelaySeconds = activationDelaySeconds;
            context.Input.hasMasterControllerState = true;
            context.Input.masterController = new EbMasterControllerInput
            {
                powerPosition = 0,
                brakePosition = 0,
                reverserPosition = ReverserPosition.Neutral,
                isInputEnabled = true,
                isActiveCab = true,
                speedMps = 10f
            };
            return context;
        }
    }
}
