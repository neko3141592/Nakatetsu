using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Simulation.Brake;

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
        public void DeviceCalculatesPressureFromSharedCylinderDefinition()
        {
            var root = new GameObject("BrakeControlDevice");
            var definition = ScriptableObject.CreateInstance<BrakeCylinderDefinitionAsset>();
            try
            {
                BrakeControlDevice device = root.AddComponent<BrakeControlDevice>();
                device.Configure(definition, 2);
                device.SetSimulationMeasurement(4500f, 2);
                device.SetTargetBrakeForceN(4500f);
                device.CollectInput();
                device.Calculate(1f);
                device.ApplyOutput(1f);

                Assert.That(device.CylinderCount, Is.EqualTo(2));
                Assert.That(device.TargetPressureKPa, Is.EqualTo(250f).Within(0.001f));
                Assert.That(device.ActualBrakeForceN, Is.EqualTo(4500f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
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
