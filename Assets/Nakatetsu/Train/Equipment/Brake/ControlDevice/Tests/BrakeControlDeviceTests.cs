using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Equipment.Brake.Cylinder;

namespace Nakatetsu.Train.Tests
{
    public sealed class BrakeControlDeviceTests
    {
        [Test]
        public void TargetForceIsConvertedToCommonCylinderPressure()
        {
            var context = new BrakeControlDeviceContext();
            context.Input.targetBrakeForceN = 4500f;
            context.Input.cylinders.Add(CreateCylinderInput());
            context.Input.cylinders.Add(CreateCylinderInput());

            BrakeControlDeviceLogic.Calculate(context);

            Assert.That(context.Output.hasOperationalCylinders, Is.True);
            Assert.That(context.Output.targetPressureKPa, Is.EqualTo(250f));
            Assert.That(context.Output.maximumBrakeForceN, Is.EqualTo(9000f));
        }

        [Test]
        public void DeviceCollectsChildCylindersAndStepsAllOfThem()
        {
            var root = new GameObject("BrakeControlDevice");
            try
            {
                new GameObject("Cylinder 1").transform.SetParent(root.transform, false);
                new GameObject("Cylinder 2").transform.SetParent(root.transform, false);
                root.transform.GetChild(0).gameObject.AddComponent<BrakeCylinder>();
                root.transform.GetChild(1).gameObject.AddComponent<BrakeCylinder>();
                BrakeControlDevice device = root.AddComponent<BrakeControlDevice>();

                device.RefreshBrakeCylinders();
                device.SetTargetBrakeForceN(4500f);
                device.Step(1f);

                Assert.That(device.BrakeCylinders.Count, Is.EqualTo(2));
                Assert.That(device.TargetPressureKPa, Is.EqualTo(250f));
                Assert.That(device.ActualBrakeForceN, Is.EqualTo(4500f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static BrakeControlCylinderInput CreateCylinderInput()
        {
            return new BrakeControlCylinderInput
            {
                isHealthy = true,
                maximumPressureKPa = 500f,
                pistonAreaM2 = 0.01f,
                mechanicalEfficiency = 0.9f
            };
        }
    }
}
