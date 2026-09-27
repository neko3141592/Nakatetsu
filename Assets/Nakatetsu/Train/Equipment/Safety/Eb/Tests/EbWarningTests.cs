using Nakatetsu.Train.Equipment.Safety.Eb;
using NUnit.Framework;

namespace Nakatetsu.Train.Tests
{
    public sealed class EbWarningTests
    {
        [TestCase(59f, false, false, 6f)]
        [TestCase(59.99f, false, false, 5.01f)]
        [TestCase(60f, true, false, 5f)]
        [TestCase(64.99f, true, false, 0.01f)]
        [TestCase(65f, true, true, 0f)]
        [TestCase(120f, true, true, 0f)]
        public void DefaultTimingHasFiveSecondWarningBeforeEmergency(
            float seconds, bool buzzer, bool emergency, float remaining)
        {
            var context = RunningContext();
            EbDeviceLogic.Calculate(context, seconds);
            Assert.That(context.Output.isBuzzerRequested, Is.EqualTo(buzzer));
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.EqualTo(emergency));
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(remaining).Within(0.0001f));
        }

        [Test]
        public void TickCrossingWarningBoundaryKeepsElapsedTime()
        {
            var context = RunningContext();
            EbDeviceLogic.Calculate(context, 59f);
            EbDeviceLogic.Calculate(context, 2f);
            Assert.That(context.Output.isBuzzerRequested, Is.True);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(4f));
            EbDeviceLogic.Calculate(context, 4f);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.True);
            Assert.That(context.Output.isBuzzerRequested, Is.True);
        }

        [TestCase("button")]
        [TestCase("master")]
        public void OperationOnLastGraceTickStopsBuzzerAndRestartsFullTimer(string operation)
        {
            var context = RunningContext();
            EbDeviceLogic.Calculate(context, 64f);
            if (operation == "button") context.Input.resetRequested = true;
            else context.Input.masterController.brakePosition = 1;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isBuzzerRequested, Is.False);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.remainingSeconds, Is.EqualTo(65f));
            context.Input.resetRequested = false;
            EbDeviceLogic.Calculate(context, 59f);
            Assert.That(context.Output.isBuzzerRequested, Is.False);
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isBuzzerRequested, Is.True);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
        }

        [TestCase("inactive")]
        [TestCase("missing")]
        [TestCase("slow")]
        public void LeavingMonitoringConditionsDuringGraceStopsWarning(string condition)
        {
            var context = RunningContext();
            EbDeviceLogic.Calculate(context, 62f);
            if (condition == "inactive") context.Input.masterController.isActiveCab = false;
            else if (condition == "missing") context.Input.hasMasterControllerState = false;
            else context.Input.masterController.speedMps = 1f;
            EbDeviceLogic.Calculate(context, 10f);
            Assert.That(context.Output.isBuzzerRequested, Is.False);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
            Assert.That(context.Output.inactivitySeconds, Is.Zero);
        }

        [Test]
        public void BuzzerPersistsWithEmergencyUntilStoppedWithoutPower()
        {
            var context = RunningContext();
            EbDeviceLogic.Calculate(context, 65f);
            context.Input.resetRequested = true;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isBuzzerRequested, Is.True);
            context.Input.masterController.speedMps = 0f;
            context.Input.masterController.powerPosition = 1;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isBuzzerRequested, Is.True);
            context.Input.masterController.powerPosition = 0;
            EbDeviceLogic.Calculate(context, 1f);
            Assert.That(context.Output.isBuzzerRequested, Is.False);
            Assert.That(context.Output.isEmergencyBrakeRequested, Is.False);
        }

        private static EbDeviceContext RunningContext()
        {
            var context = new EbDeviceContext();
            context.Input.hasMasterControllerState = true;
            context.Input.masterController = new EbMasterControllerInput
            {
                isActiveCab = true, isInputEnabled = true, speedMps = 10f
            };
            return context;
        }
    }
}
