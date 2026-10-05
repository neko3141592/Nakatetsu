using System;
using NUnit.Framework;

namespace Nakatetsu.Train.Equipment.Operation.Tests
{
    public sealed class MasterControllerLogicTests
    {
        [Test]
        public void MissingKeyInitializesAtEmergencyBrakeAndNeutralReverser()
        {
            var context = new MasterControllerContext();
            MasterControllerLogic.Initialize(context, ReverserPosition.Forward, true);

            Assert.That(context.State.isKeyInserted, Is.False);
            AssertParked(context);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NeutralReverserLocksEveryHandleOperation(bool keyInserted)
        {
            // 直接指定と一段ずつの操作を、同じ鎖錠条件で拒否する。
            Action<MasterControllerContext>[] operations =
            {
                context => MasterControllerLogic.SetPowerPosition(context, 3),
                context => MasterControllerLogic.SetBrakePosition(context, 0),
                context => MasterControllerLogic.SetServiceBrakePosition(context, 4),
                MasterControllerLogic.SetNeutral,
                MasterControllerLogic.MoveOneStepTowardBrake,
                MasterControllerLogic.MoveOneStepTowardPower,
                MasterControllerLogic.StepTowardNeutral,
                MasterControllerLogic.StepTowardServiceMaxBrake
            };
            foreach (var operation in operations)
            {
                var context = NewContext(keyInserted);
                operation(context);
                AssertParked(context);
            }
        }

        [Test]
        public void MissingKeyLocksReverser()
        {
            var context = NewContext(false);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Forward), Is.False);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Reverse), Is.False);
            AssertParked(context);
        }

        [TestCase(ReverserPosition.Forward)]
        [TestCase(ReverserPosition.Reverse)]
        public void KeyAndSelectedDirectionAllowHandleOperation(ReverserPosition direction)
        {
            var context = NewContext(false);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, true), Is.True);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, direction), Is.True);
            MasterControllerLogic.MoveOneStepTowardPower(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(7));
            MasterControllerLogic.SetNeutral(context);
            MasterControllerLogic.SetPowerPosition(context, 3);
            Assert.That(context.State.powerPosition, Is.EqualTo(3));
            Assert.That(context.State.brakePosition, Is.Zero);
            MasterControllerLogic.SetServiceBrakePosition(context, 4);
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(4));
        }

        [Test]
        public void KeyCanOnlyBeRemovedAfterReturningToEmergencyBrakeAndNeutralReverser()
        {
            var context = NewContext(true);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Forward), Is.True);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, false), Is.False);
            MasterControllerLogic.SetPowerPosition(context, 2);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Neutral), Is.False);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, false), Is.False);
            Assert.That(context.State.isKeyInserted, Is.True);
            MasterControllerLogic.SetEmergencyBrake(context);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, false), Is.False);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Neutral), Is.True);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, false), Is.True);
            MasterControllerLogic.MoveOneStepTowardPower(context);
            AssertParked(context);
            Assert.That(MasterControllerLogic.TrySetKeyInserted(context, true), Is.True);
        }

        [Test]
        public void ServiceBrakeAlsoLocksReverser()
        {
            var context = NewContext(true);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Forward), Is.True);
            MasterControllerLogic.SetServiceBrakePosition(context, 7);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Reverse), Is.False);
            Assert.That(context.State.reverserPosition, Is.EqualTo(ReverserPosition.Forward));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangingLimitsKeepsLockedHandleAtNewEmergencyPosition(bool keyInserted)
        {
            var context = NewContext(keyInserted);
            MasterControllerLogic.ConfigureLimits(context, 5, 9, 11);
            AssertParked(context);
        }

        [Test]
        public void DisabledInputCannotReleaseBrakeButCanSetEmergencyBrake()
        {
            var context = NewContext(true);
            Assert.That(MasterControllerLogic.TrySetReverserPosition(context, ReverserPosition.Forward), Is.True);
            MasterControllerLogic.SetPowerPosition(context, 2);
            context.State.isInputEnabled = false;
            MasterControllerLogic.SetNeutral(context);
            Assert.That(context.State.powerPosition, Is.EqualTo(2));
            MasterControllerLogic.SetEmergencyBrake(context);
            Assert.That(context.State.powerPosition, Is.Zero);
            MasterControllerLogic.MoveOneStepTowardPower(context);
            Assert.That(context.State.brakePosition, Is.EqualTo(context.Settings.emergencyBrakePosition));
        }

        private static MasterControllerContext NewContext(bool keyInserted)
        {
            var context = new MasterControllerContext();
            MasterControllerLogic.Initialize(context, ReverserPosition.Neutral, true, keyInserted);
            return context;
        }

        private static void AssertParked(MasterControllerContext context)
        {
            Assert.That(context.State.powerPosition, Is.Zero);
            Assert.That(context.State.brakePosition, Is.EqualTo(context.Settings.emergencyBrakePosition));
            Assert.That(context.State.reverserPosition, Is.EqualTo(ReverserPosition.Neutral));
        }
    }
}
