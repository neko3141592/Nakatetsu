using NUnit.Framework;
using Nakatetsu.Train.Equipment.Tims.Brake;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class TimsBrakeNewLogicTests
    {
        [Test]
        public void Calculate_DistributesRemainingForceByMassWithoutScalingMinimumPressure()
        {
            TimsBrakeContext context = CreateContext();

            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.minimumAirPressureKPa, Is.EqualTo(40f));
            Assert.That(context.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 4000f, 4000f }));
            Assert.That(context.Workspace.remainingTargetBrakeForceN, Is.EqualTo(72000f));
            Assert.That(context.Workspace.targetCarBrakeForcesN, Is.EqualTo(new[] { 27000f, 45000f }));
        }

        [Test]
        public void Calculate_ReleaseClearsPreviousCarTargets()
        {
            TimsBrakeContext context = CreateContext();
            TimsBrakeNewLogic.Calculate(context);

            context.Input.brakeStep = 0;
            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.targetCarBrakeForcesN, Is.EqualTo(new[] { 0f, 0f }));
        }

        [Test]
        public void Calculate_ZeroTotalMassProducesZeroCarTargets()
        {
            TimsBrakeContext context = CreateContext();
            foreach (TimsBrakeCarInput carInput in context.Input.cars)
            {
                carInput.massKg = 0f;
            }

            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.targetCarBrakeForcesN, Is.EqualTo(new[] { 0f, 0f }));
        }

        private static TimsBrakeContext CreateContext()
        {
            var context = new TimsBrakeContext();
            context.Input.canReleaseEmergencyBrake = true;
            context.Input.brakeStep = 1;
            context.Settings.brakeTargetDecelerationsMps2.Add(1f);
            context.Settings.minimumServiceBrakePressureKPa = 40f;
            context.Input.cars.Add(new TimsBrakeCarInput { massKg = 30000f, airForcePerKPa = 100f });
            context.Input.cars.Add(new TimsBrakeCarInput { massKg = 50000f, airForcePerKPa = 100f });
            return context;
        }
    }
}
