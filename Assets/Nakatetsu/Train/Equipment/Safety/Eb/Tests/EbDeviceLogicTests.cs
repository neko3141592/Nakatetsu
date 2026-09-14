using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using NUnit.Framework;
using UnityEngine;

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
        public void DeviceCopiesAssignedMasterControllerStateIntoInput()
        {
            var trainObject = new GameObject("Train");
            var masterObject = new GameObject("MasterController");
            var ebObject = new GameObject("EbDevice");
            try
            {
                trainObject.AddComponent<TrainRoot>();
                masterObject.transform.SetParent(trainObject.transform);
                ebObject.transform.SetParent(trainObject.transform);

                TrainEquipmentAssignment masterAssignment =
                    masterObject.AddComponent<TrainEquipmentAssignment>();
                masterAssignment.AssignCarIndex(0);
                MasterController masterController = masterObject.AddComponent<MasterController>();
                masterController.ConfigureLimits(4, 7, 8);
                masterController.SetPowerPosition(2);

                TrainEquipmentAssignment ebAssignment =
                    ebObject.AddComponent<TrainEquipmentAssignment>();
                ebAssignment.AssignCarIndex(0);
                EbDevice ebDevice = ebObject.AddComponent<EbDevice>();

                ebDevice.Step(1f);

                Assert.That(ebDevice.Context.Input.hasMasterControllerState, Is.True);
                Assert.That(ebDevice.Context.Input.masterController.powerPosition, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(trainObject);
            }
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
