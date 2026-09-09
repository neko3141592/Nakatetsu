using NUnit.Framework;
using Nakatetsu.Train.Operation;

namespace Nakatetsu.Train.Tests
{
    public sealed class MasterControllerLogicTests
    {
        private static MasterControllerContext Create()
        {
            var context = new MasterControllerContext();
            MasterControllerLogic.ConfigureLimits(context, 4, 7, 8);
            MasterControllerLogic.Initialize(context, ReverserPosition.Neutral, true);
            return context;
        }

        [Test]
        public void PowerAndBrakeAreMutuallyExclusive()
        {
            MasterControllerContext context = Create();

            MasterControllerLogic.SetPowerPosition(context, 3);
            Assert.That(context.State.powerPosition, Is.EqualTo(3));
            Assert.That(context.State.brakePosition, Is.Zero);

            MasterControllerLogic.SetBrakePosition(context, 2);
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(2));
        }

        [Test]
        public void CombinedHandleMovesThroughNeutral()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 2);

            MasterControllerLogic.MoveOneStepTowardBrake(context);
            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.Zero);

            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(1));

            MasterControllerLogic.MoveOneStepTowardPower(context);
            Assert.That(context.State.brakePosition, Is.Zero);
        }

        [Test]
        public void ServiceStepDoesNotEnterEmergencyButDirectBrakeStepCan()
        {
            MasterControllerContext context = Create();

            for (int i = 0; i < 20; i++)
            {
                MasterControllerLogic.StepTowardServiceMaxBrake(context);
            }

            Assert.That(context.State.brakePosition, Is.EqualTo(7));
            MasterControllerLogic.MoveOneStepTowardBrake(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(8));
        }

        [Test]
        public void ReverserCannotChangeWhilePowerIsApplied()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 1);

            bool accepted = MasterControllerLogic.TrySetReverserPosition(
                context,
                ReverserPosition.Forward);

            Assert.That(accepted, Is.False);
            Assert.That(context.State.reverserPosition, Is.EqualTo(ReverserPosition.Neutral));
        }

        [Test]
        public void EmergencyBrakeClearsPowerAndUsesConfiguredPosition()
        {
            MasterControllerContext context = Create();
            MasterControllerLogic.SetPowerPosition(context, 4);

            MasterControllerLogic.SetEmergencyBrake(context);

            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(8));
        }
    }
}
