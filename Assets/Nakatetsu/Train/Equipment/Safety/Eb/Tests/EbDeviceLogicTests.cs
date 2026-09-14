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
                isInputEnabled = true
            };
            return context;
        }
    }
}
