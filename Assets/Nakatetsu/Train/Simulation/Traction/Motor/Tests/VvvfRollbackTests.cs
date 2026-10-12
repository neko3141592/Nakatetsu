using System.Reflection;
using Nakatetsu.Train.Equipment.Traction;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Equipment.Traction.Vvvf;
using Nakatetsu.Train.Simulation.Physics;
using Nakatetsu.Train.Simulation.Traction.Motor;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Tests
{
    public sealed class VvvfRollbackTests
    {
        [TestCase(1, 10f)]
        [TestCase(-1, -10f)]
        public void PowerStopsRollbackAndStartsInCommandedDirection(int direction, float gradient)
        {
            var root = new GameObject("RollbackTest");
            var vvvfDefinition = ScriptableObject.CreateInstance<VvvfDefinitionAsset>();
            var motorDefinition = ScriptableObject.CreateInstance<MotorDefinitionAsset>();
            var driveDefinition = ScriptableObject.CreateInstance<TrainDriveDefinition>();
            try
            {
                var controller = root.AddComponent<VvvfController>();
                controller.Configure(vvvfDefinition, driveDefinition);
                controller.ConfigureMotor(motorDefinition, driveDefinition, 4);
                typeof(VvvfController).GetField("tractionCommandSource", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, new PowerCommand(direction));
                var motor = root.AddComponent<TrainMotorSimulation>();
                motor.Configure(motorDefinition, driveDefinition, 4);
                var physics = new TrainPhysicsContext();
                physics.Input.totalMassKg = 294000f;
                physics.Input.totalGradeForceN = TrainPhysicsLogic.CalculateGradeForceN(294000f, gradient);
                physics.Input.totalRunningResistanceAN = 4000f;
                physics.State.signedVelocityMps = -direction * 0.02f;

                for (int tick = 0; tick < 150; tick++)
                {
                    float velocity = physics.State.signedVelocityMps;
                    controller.SetVehicleSpeedMps(velocity);
                    controller.SetMotorMeasurement(motor.AverageMotorTorqueNm, motor.ActualTractionForceN,
                        motor.TotalMotorCurrentRmsA, motor.TotalMotorOutputPowerW);
                    controller.CollectInput();
                    controller.Calculate(0.02f);
                    motor.SetInput(controller.Output.lineVoltageRmsV, controller.Output.frequencyHz, velocity);
                    motor.Calculate(0.02f);
                    physics.Input.totalTractionForceN = Mathf.Max(0f, motor.ActualTractionForceN) * direction * 6;
                    physics.Input.totalBrakeForceN = Mathf.Max(0f, -motor.ActualTractionForceN) * 6;
                    TrainPhysicsLogic.Calculate(physics, 0.02f);
                }

                Assert.That(controller.TargetTractionForceN, Is.GreaterThan(0f));
                Assert.That(controller.ForceDirectionSign, Is.EqualTo(direction));
                Assert.That(physics.State.signedVelocityMps * direction, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(vvvfDefinition);
                Object.DestroyImmediate(motorDefinition);
                Object.DestroyImmediate(driveDefinition);
            }
        }

        private sealed class PowerCommand : ITractionCommandSource, ITractionDirectionSource
        {
            private readonly int direction;
            public PowerCommand(int direction) => this.direction = direction;
            public bool TryGetTargetTractionForceN(out float force) { force = 38000f; return true; }
            public bool TryGetTractionDirection(out int value) { value = direction; return true; }
        }
    }
}
