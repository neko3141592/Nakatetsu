using System.Reflection;
using Nakatetsu.Train.Equipment.Safety.Eb;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Tests
{
    public sealed class EbDeviceResetTests
    {
        private GameObject deviceObject;
        private EbDevice device;

        [SetUp]
        public void SetUp()
        {
            deviceObject = new GameObject("EB reset test");
            device = deviceObject.AddComponent<EbDevice>();
            device.Configure(new RunningCabInput(), 10f);
            Tick(8f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(deviceObject);
        }

        [Test]
        public void RequestIsDeferredAndConsumedOnce()
        {
            device.RequestReset();
            device.RequestReset();
            Assert.That(device.Output.inactivitySeconds, Is.EqualTo(8f));
            Tick(2f);
            Assert.That(device.Output.inactivitySeconds, Is.Zero);
            Assert.That(device.IsEmergencyBrakeRequested, Is.False);
            Tick(1f);
            Assert.That(device.Output.inactivitySeconds, Is.EqualTo(1f));
            Tick(14f);
            Assert.That(device.IsEmergencyBrakeRequested, Is.True);
        }

        [Test]
        public void RequestAfterCollectionWaitsForNextTick()
        {
            device.CollectInput();
            device.RequestReset();
            device.Calculate(1f);
            Assert.That(device.Output.inactivitySeconds, Is.EqualTo(9f));
            Tick(1f);
            Assert.That(device.Output.inactivitySeconds, Is.Zero);
            Tick(1f);
            Assert.That(device.Output.inactivitySeconds, Is.EqualTo(1f));
        }

        [Test]
        public void DisablingDiscardsPendingAndRejectsNewRequests()
        {
            device.RequestReset();
            device.enabled = false;
            // EditModeではOnDisableが自動実行されないため、ライフサイクルを明示する。
            typeof(EbDevice).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(device, null);
            device.RequestReset();
            device.enabled = true;
            Tick(1f);
            Assert.That(device.Output.inactivitySeconds, Is.EqualTo(9f));
        }

        [Test]
        public void ResetDuringEmergencyIsConsumedWithoutReleasing()
        {
            Tick(7f);
            device.RequestReset();
            Tick(1f);
            Assert.That(device.IsEmergencyBrakeRequested, Is.True);
            Assert.That(device.Context.Input.resetRequested, Is.False);
        }

        private void Tick(float seconds)
        {
            device.CollectInput();
            device.Calculate(seconds);
            device.ApplyOutput(seconds);
        }

        private sealed class RunningCabInput : IEbMasterControllerInputSource
        {
            public bool TryReadMasterControllerInput(out EbMasterControllerInput input)
            {
                input = new EbMasterControllerInput
                {
                    isActiveCab = true,
                    isInputEnabled = true,
                    speedMps = 10f
                };
                return true;
            }
        }
    }
}
