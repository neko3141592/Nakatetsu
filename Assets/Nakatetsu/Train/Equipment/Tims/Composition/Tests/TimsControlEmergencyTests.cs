using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Safety.Eb;
using Nakatetsu.Train.Equipment.Tims.Speed;
using Nakatetsu.Train.Simulation.Brake;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class TimsControlEmergencyTests
    {
        private GameObject trainObject;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset car;
        private BrakeCylinderDefinitionAsset cylinder;
        private TimsSettingsAsset settings;
        private TimsCommunicationController communication;
        private TimsControlController control;

        [SetUp]
        public void SetUp()
        {
            trainObject = new GameObject("Emergency regression train");
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            cylinder = ScriptableObject.CreateInstance<BrakeCylinderDefinitionAsset>();
            car.emptyMassKg = 30000f;
            car.brakeCylinderDefinition = cylinder;
            consist.cars.Add(car);
            consist.cars.Add(car);
            TrainRoot root = trainObject.AddComponent<TrainRoot>();
            root.Configure(consist);

            settings = ScriptableObject.CreateInstance<TimsSettingsAsset>();
            for (int notch = 1; notch <= settings.brakeNotchCount; notch++)
            {
                settings.brakeTargetDecelerationsKmhPerSec.Add(notch * 0.5f);
            }

            var timsObject = new GameObject("TIMS");
            timsObject.transform.SetParent(trainObject.transform);
            control = timsObject.AddComponent<TimsControlController>();
            timsObject.GetComponent<TimsRoot>().Configure(settings);
            communication = timsObject.GetComponent<TimsCommunicationController>();
            typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(communication, null);

            for (int index = 0; index < consist.CarCount; index++)
            {
                TimsBusState bus = communication.GetLocalBus(index);
                bus.SetInt(CabActivationSwitchTimsBusSource.SwitchPositionKey,
                    (int)(index == 0 ? CabActivationPosition.Front : CabActivationPosition.Rear));
                bus.SetInt(MasterControllerTimsBusSource.ReverserPositionKey, (int)ReverserPosition.Forward);
                bus.SetInt(MasterControllerTimsBusSource.PowerPositionKey, 0);
                bus.SetInt(MasterControllerTimsBusSource.BrakePositionKey, 0);
                bus.SetFloat(BrakeControlDeviceTimsBusSource.MassKgKey, 30000f);
                bus.SetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey, 0f);
                bus.SetFloat(BrakeControlDeviceTimsBusSource.ActualForceNKey, 0f);
                bus.SetFloat(BrakeControlDeviceTimsBusSource.ForcePerKPaKey, 40f);
                bus.SetFloat(BrakeControlDeviceTimsBusSource.MaximumPressureKPaKey, 500f);
                bus.SetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, false);
            }

            communication.MasterBus.SetFloat(TimsSpeedController.SpeedMpsKey, 10f);
            AssertEmergency(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(trainObject);
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(consist);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(cylinder);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void ManualEmergencyReleasesWhileMovingWhenNotchIsReturned(int brakeNotch)
        {
            TimsBusState cab = communication.GetLocalBus(0);
            cab.SetInt(MasterControllerTimsBusSource.BrakePositionKey, 8);
            AssertEmergency(true);
            cab.SetInt(MasterControllerTimsBusSource.BrakePositionKey, brakeNotch);
            AssertEmergency(false);
        }

        [Test]
        public void DeviceRequestControlsEmergencyWithoutTimsKeepingPreviousValue()
        {
            TimsBusState cab = communication.GetLocalBus(0);
            cab.SetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, true);
            AssertEmergency(true);
            cab.SetInt(MasterControllerTimsBusSource.BrakePositionKey, 7);
            AssertEmergency(true);
            cab.SetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, false);
            AssertEmergency(false);
        }

        [Test]
        public void RestoredInputClearsItsEmergencyRequestWhileMoving()
        {
            communication.MasterBus.Remove(TimsSpeedController.SpeedMpsKey);
            AssertEmergency(true);
            communication.MasterBus.SetFloat(TimsSpeedController.SpeedMpsKey, 10f);
            AssertEmergency(false);
        }

        private void AssertEmergency(bool expected)
        {
            control.CalculateAndPublish();
            Assert.That(communication.MasterBus.TryGetBool(TimsBrakeController.IsEmergencyKey, out bool actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected), control.EmergencyReason);
        }
    }
}
