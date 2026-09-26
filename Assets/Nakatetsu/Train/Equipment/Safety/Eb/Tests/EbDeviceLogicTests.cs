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

            EbDeviceLogic.Calculate(context, 11f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(15f));
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
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(15f));
        }

        [Test]
        public void ResetButtonRestartsTimerBeforeActivationEvenOnExpiryTick()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 14f);

            context.Input.resetRequested = true;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(15f));

            context.Input.resetRequested = false;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(1f));
        }

        [Test]
        public void DisabledMasterControllerStopsMonitoring()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 8f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);

            context.Input.masterController.isInputEnabled = false;
            EbDeviceLogic.Calculate(context, 1f);

            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
        }

        [Test]
        public void InactiveCabResetsAndDoesNotAccumulateTime()
        {
            EbDeviceContext context = CreateContext(10f);
            EbDeviceLogic.Calculate(context, 8f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);

            context.Input.masterController.isActiveCab = false;
            EbDeviceLogic.Calculate(context, 100f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(15f));

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
            EbDeviceLogic.Calculate(context, 65f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.EqualTo(isRunning));
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(isRunning ? 65f : 0f));
        }

        [Test]
        public void SlowingBelowThresholdBeforeActivationRestartsTimerFromZero()
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 59f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);

            context.Input.masterController.speedMps = 4f / 3.6f;
            EbDeviceLogic.Calculate(context, 120f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(65f));

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
            EbDeviceLogic.Calculate(context, 6f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidSpeedClearsMonitoring(float speedMps)
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 59f);
            context.Input.masterController.speedMps = speedMps;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
        }

        [TestCase("power")]
        [TestCase("brake")]
        [TestCase("reverser")]
        [TestCase("disabled")]
        [TestCase("inactive")]
        [TestCase("missing")]
        [TestCase("invalidSpeed")]
        [TestCase("lowSpeed")]
        [TestCase("reset")]
        public void ActivatedDeviceKeepsEmergencyUntilItsReleaseConditionsAreMet(string change)
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 65f);

            switch (change)
            {
                case "reset":
                    context.Input.resetRequested = true;
                    break;
                case "power":
                    context.Input.masterController.powerPosition = 1;
                    break;
                case "brake":
                    context.Input.masterController.brakePosition = 7;
                    break;
                case "reverser":
                    context.Input.masterController.reverserPosition = ReverserPosition.Forward;
                    break;
                case "disabled":
                    context.Input.masterController.isInputEnabled = false;
                    break;
                case "inactive":
                    context.Input.masterController.isActiveCab = false;
                    break;
                case "missing":
                    context.Input.hasMasterControllerState = false;
                    context.Input.masterController = default;
                    break;
                case "invalidSpeed":
                    context.Input.masterController.speedMps = float.NaN;
                    break;
                case "lowSpeed":
                    context.Input.masterController.speedMps = 4f / 3.6f;
                    break;
            }

            EbDeviceLogic.Calculate(context, 1f);

            Assert.That(context.State.isEmergencyBrakeLatched, Is.True);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(65f));
            Assert.That(context.Output.remainingSeconds, Is.Zero);
        }

        [TestCase(0f, 0, 0, true)]
        [TestCase(0f, 0, 7, true)]
        [TestCase(0f, 0, 8, true)]
        [TestCase(0f, 1, 0, false)]
        [TestCase(0.009f, 0, 0, true)]
        [TestCase(-0.009f, 0, 0, true)]
        [TestCase(0.01f, 0, 0, false)]
        [TestCase(-0.01f, 0, 0, false)]
        public void EmergencyReleasesOnlyWhenStoppedWithoutPower(
            float speedMps, int power, int brake, bool releases)
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 65f);
            context.Input.masterController.speedMps = speedMps;
            context.Input.masterController.powerPosition = power;
            context.Input.masterController.brakePosition = brake;

            EbDeviceLogic.Calculate(context, 1f);

            Assert.That(context.State.isEmergencyBrakeLatched, Is.EqualTo(!releases));
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.EqualTo(!releases));
        }

        [TestCase(10f, 0, false)]
        [TestCase(0f, 1, false)]
        [TestCase(0f, 0, true)]
        public void ResetButtonDoesNotBypassEmergencyReleaseConditions(float speedMps, int power, bool releases)
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 65f);
            context.Input.masterController.speedMps = speedMps;
            context.Input.masterController.powerPosition = power;
            context.Input.resetRequested = true;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.EqualTo(!releases));
        }

        [Test]
        public void ReleasedDeviceStartsMonitoringFromZeroOnNextRun()
        {
            var context = CreateContext(60f);
            EbDeviceLogic.Calculate(context, 65f);
            context.Input.masterController.speedMps = 0f;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);

            context.Input.masterController.speedMps = 10f;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.EqualTo(1f));
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(64f));
        }

        private static EbDeviceContext CreateContext(float activationDelaySeconds)
        {
            var context = new EbDeviceContext();
            context.Settings.warningDelaySeconds = activationDelaySeconds;
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
