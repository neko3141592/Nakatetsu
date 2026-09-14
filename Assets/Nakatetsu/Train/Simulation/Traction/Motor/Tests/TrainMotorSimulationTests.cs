using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Simulation.Traction.Motor;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Tests
{
    public sealed class TrainMotorSimulationTests
    {
        [Test]
        public void BuildsConfiguredMotorCountAndAggregatesPhysicalForce()
        {
            var root = new GameObject("TrainMotorSimulation");
            var motorDefinition = ScriptableObject.CreateInstance<MotorDefinitionAsset>();
            var driveDefinition = ScriptableObject.CreateInstance<TrainDriveDefinition>();
            try
            {
                TrainMotorSimulation simulation = root.AddComponent<TrainMotorSimulation>();
                simulation.Configure(motorDefinition, driveDefinition, 4);
                simulation.SetInput(500f, 20f, 0f);
                simulation.Calculate(0.02f);

                Assert.That(simulation.MotorCount, Is.EqualTo(4));
                Assert.That(simulation.ActualTractionForceN, Is.GreaterThan(0f));
                Assert.That(simulation.TotalMotorCurrentRmsA, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(motorDefinition);
                Object.DestroyImmediate(driveDefinition);
            }
        }
    }
}
