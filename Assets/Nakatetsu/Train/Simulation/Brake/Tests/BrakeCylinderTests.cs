using NUnit.Framework;
using Nakatetsu.Train.Simulation.Brake;
using UnityEngine;

namespace Nakatetsu.Train.Tests
{
    public sealed class BrakeCylinderTests
    {
        [Test]
        public void PressureFollowsTargetUsingApplyAndReleaseRates()
        {
            var context = new BrakeCylinderContext();
            context.Input.targetPressureKPa = 400f;
            context.Input.deltaTimeSeconds = 1f;

            BrakeCylinderLogic.Calculate(context);
            Assert.That(context.State.currentPressureKPa, Is.EqualTo(250f));

            context.Input.targetPressureKPa = 0f;
            context.Input.deltaTimeSeconds = 0.5f;
            BrakeCylinderLogic.Calculate(context);
            Assert.That(context.State.currentPressureKPa, Is.EqualTo(75f));
        }

        [Test]
        public void UnhealthyCylinderReleasesPressureAndReportsActualForce()
        {
            var context = new BrakeCylinderContext();
            context.State.currentPressureKPa = 300f;
            context.State.isHealthy = false;
            context.Input.targetPressureKPa = 500f;
            context.Input.deltaTimeSeconds = 1f;

            BrakeCylinderLogic.Calculate(context);

            Assert.That(context.State.currentPressureKPa, Is.Zero);
            Assert.That(context.Output.actualForceN, Is.Zero);
        }

        [Test]
        public void TrainBrakeSimulationBuildsIdenticalCylindersAndAggregatesForce()
        {
            var root = new GameObject("TrainBrakeSimulation");
            var definition = ScriptableObject.CreateInstance<BrakeCylinderDefinitionAsset>();
            try
            {
                TrainBrakeSimulation simulation = root.AddComponent<TrainBrakeSimulation>();
                simulation.Configure(definition, 4);
                simulation.SetTargetPressureKPa(250f);
                simulation.Calculate(1f);

                Assert.That(simulation.CylinderCount, Is.EqualTo(4));
                Assert.That(simulation.OperationalCylinderCount, Is.EqualTo(4));
                Assert.That(simulation.ActualBrakeForceN, Is.EqualTo(9000f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
