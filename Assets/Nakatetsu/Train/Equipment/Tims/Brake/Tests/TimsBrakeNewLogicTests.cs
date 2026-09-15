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

        [Test]
        public void Calculate_DistributesRegenEquallyToMotorCarsRegardlessOfMass()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[1].isVvvfMotorCar = true;
            context.Input.cars.Insert(1, new TimsBrakeCarInput
            {
                massKg = 20000f,
                airForcePerKPa = 100f,
                isTrailerCar = true
            });

            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.remainingTargetBrakeForceN, Is.EqualTo(88000f));
            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 44000f, 0f, 44000f }));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Calculate_MotorCarRemovalUpdatesRegenTargets(int remainingMotorCarCount)
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[1].isVvvfMotorCar = true;
            TimsBrakeNewLogic.Calculate(context);

            context.Input.cars[0].isVvvfMotorCar = false;
            context.Input.cars[1].isVvvfMotorCar = remainingMotorCarCount == 1;
            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN,
                Is.EqualTo(new[] { 0f, remainingMotorCarCount == 1 ? 72000f : 0f }));
        }

        [Test]
        public void Calculate_ReleaseClearsPreviousRegenTargets()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            TimsBrakeNewLogic.Calculate(context);

            context.Input.brakeStep = 0;
            TimsBrakeNewLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 0f, 0f }));
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
