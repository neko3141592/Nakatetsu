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

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.minimumAirPressureKPa, Is.EqualTo(40f));
            Assert.That(context.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 4000f, 4000f }));
            Assert.That(context.Workspace.remainingTargetBrakeForceN, Is.EqualTo(72000f));
            Assert.That(context.Workspace.targetCarBrakeForcesN, Is.EqualTo(new[] { 27000f, 45000f }));
        }

        [Test]
        public void Calculate_ReleaseClearsPreviousCarTargets()
        {
            TimsBrakeContext context = CreateContext();
            TimsBrakeLogic.Calculate(context);

            context.Input.brakeStep = 0;
            TimsBrakeLogic.Calculate(context);

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

            TimsBrakeLogic.Calculate(context);

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
                isTrailerCar = true,
                regenCapN = 100000f
            });

            TimsBrakeLogic.Calculate(context);

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
            TimsBrakeLogic.Calculate(context);

            context.Input.cars[0].isVvvfMotorCar = false;
            context.Input.cars[1].isVvvfMotorCar = remainingMotorCarCount == 1;
            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN,
                Is.EqualTo(new[] { 0f, remainingMotorCarCount == 1 ? 72000f : 0f }));
        }

        [Test]
        public void Calculate_ReleaseClearsPreviousRegenTargets()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            TimsBrakeLogic.Calculate(context);

            context.Input.brakeStep = 0;
            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 0f, 0f }));
        }

        [TestCase(10000f, 100000f, 10000f, 62000f)]
        [TestCase(100000f, 10000f, 62000f, 10000f)]
        [TestCase(10000f, 20000f, 10000f, 20000f)]
        [TestCase(0f, 100000f, 0f, 72000f)]
        [TestCase(-1000f, 100000f, 0f, 72000f)]
        [TestCase(0f, 0f, 0f, 0f)]
        public void Calculate_RegenRespectsCapsAndRedistributesRemainingForce(
            float firstCapN, float secondCapN, float firstTargetN, float secondTargetN)
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[1].isVvvfMotorCar = true;
            context.Input.cars[0].regenCapN = firstCapN;
            context.Input.cars[1].regenCapN = secondCapN;

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN,
                Is.EqualTo(new[] { firstTargetN, secondTargetN }).Within(0.01f));
            // 空制補足でも使う必要制動力と車両別の荷重配分は保持する。
            Assert.That(context.Workspace.remainingTargetBrakeForceN, Is.EqualTo(72000f));
            Assert.That(context.Workspace.targetCarBrakeForcesN, Is.EqualTo(new[] { 27000f, 45000f }));
        }

        [Test]
        public void Calculate_RedistributesRegenAfterMultipleCarsReachTheirCaps()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[0].regenCapN = 5000f;
            context.Input.cars[1].isVvvfMotorCar = true;
            context.Input.cars[1].regenCapN = 30000f;
            context.Input.cars.Add(new TimsBrakeCarInput
            {
                massKg = 20000f,
                airForcePerKPa = 100f,
                isVvvfMotorCar = true,
                regenCapN = 100000f
            });

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN,
                Is.EqualTo(new[] { 5000f, 30000f, 53000f }).Within(0.01f));
        }

        [Test]
        public void Calculate_CapacityLossClearsPreviousRegenTarget()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[1].isVvvfMotorCar = true;
            TimsBrakeLogic.Calculate(context);

            context.Input.cars[0].regenCapN = 0f;
            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 0f, 72000f }));
        }

        [TestCase(0f, 27000f, 45000f)]
        [TestCase(20000f, 7000f, 45000f)]
        [TestCase(50000f, 0f, 22000f)]
        [TestCase(100000f, 0f, 0f)]
        public void Calculate_ActualRegenReducesAdditionalAirWithoutReducingMinimumAir(
            float actualRegenN, float firstAirN, float secondAirN)
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[0].regenForceN = actualRegenN;

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 72000f, 0f }));
            Assert.That(context.Workspace.additionalAirForcesN,
                Is.EqualTo(new[] { firstAirN, secondAirN }).Within(0.01f));
            Assert.That(context.Workspace.minimumAirPressureKPa, Is.EqualTo(40f));
            Assert.That(context.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 4000f, 4000f }));
        }

        [Test]
        public void Calculate_ActualRegenIsUsedEvenWhenNewRegenCommandIsZero()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[0].regenCapN = 0f;
            context.Input.cars[0].regenForceN = 20000f;

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.targetRegenForcesN, Is.EqualTo(new[] { 0f, 0f }));
            Assert.That(context.Workspace.additionalAirForcesN, Is.EqualTo(new[] { 7000f, 45000f }));
        }

        [Test]
        public void Calculate_SurplusRegenRedistributesAfterAnotherCarNeedsNoAdditionalAir()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[0].regenForceN = 80000f;
            context.Input.cars[1].isVvvfMotorCar = true;
            context.Input.cars.Add(new TimsBrakeCarInput
            {
                massKg = 20000f,
                airForcePerKPa = 100f,
                isTrailerCar = true
            });

            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.additionalAirForcesN,
                Is.EqualTo(new[] { 0f, 8000f, 0f }).Within(0.01f));
            Assert.That(context.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 4000f, 4000f, 4000f }));
        }

        [Test]
        public void Calculate_RegenLossRestoresAirAndReleaseClearsIt()
        {
            TimsBrakeContext context = CreateContext();
            context.Input.cars[0].isVvvfMotorCar = true;
            context.Input.cars[0].regenForceN = 50000f;
            TimsBrakeLogic.Calculate(context);

            context.Input.cars[0].regenForceN = 0f;
            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.additionalAirForcesN, Is.EqualTo(new[] { 27000f, 45000f }));

            context.Input.brakeStep = 0;
            TimsBrakeLogic.Calculate(context);

            Assert.That(context.Workspace.additionalAirForcesN, Is.EqualTo(new[] { 0f, 0f }));
            Assert.That(context.Workspace.minimumAirForcesN, Is.EqualTo(new[] { 0f, 0f }));
        }

        private static TimsBrakeContext CreateContext()
        {
            var context = new TimsBrakeContext();
            context.Input.isEmergencyBrakeRequested = false;
            context.Input.brakeStep = 1;
            context.Settings.brakeTargetDecelerationsMps2.Add(1f);
            context.Settings.minimumServiceBrakePressureKPa = 40f;
            context.Input.cars.Add(new TimsBrakeCarInput { massKg = 30000f, airForcePerKPa = 100f, regenCapN = 100000f });
            context.Input.cars.Add(new TimsBrakeCarInput { massKg = 50000f, airForcePerKPa = 100f, regenCapN = 100000f });
            return context;
        }
    }
}
