using NUnit.Framework;
using Nakatetsu.Train.Equipment.Operation.Switches;

namespace Nakatetsu.Train.Tests
{
    public sealed class PositionSwitchTests
    {
        [Test]
        public void InitializeClampsPositionToConfiguredRange()
        {
            var context = new PositionSwitchContext();
            PositionSwitchLogic.Configure(context, -1, 1, false, 0);

            PositionSwitchLogic.Initialize(context, 4);

            Assert.That(context.State.position, Is.EqualTo(1));
        }

        [Test]
        public void ConfigureAcceptsRangeInEitherOrder()
        {
            var context = new PositionSwitchContext();

            PositionSwitchLogic.Configure(context, 2, -2, false, 0);

            Assert.That(context.Settings.minimumPosition, Is.EqualTo(-2));
            Assert.That(context.Settings.maximumPosition, Is.EqualTo(2));
        }

        [Test]
        public void MoveOneStepStopsAtPositionLimits()
        {
            var context = new PositionSwitchContext();
            PositionSwitchLogic.Configure(context, 0, 2, false, 0);
            PositionSwitchLogic.Initialize(context, 1);

            Assert.That(PositionSwitchLogic.MoveOneStep(context, 1), Is.True);
            Assert.That(context.State.position, Is.EqualTo(2));
            Assert.That(PositionSwitchLogic.MoveOneStep(context, 1), Is.False);
            Assert.That(context.State.position, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseReturnsMomentarySwitchToRestPosition()
        {
            var context = new PositionSwitchContext();
            PositionSwitchLogic.Configure(context, -1, 1, true, 0);
            PositionSwitchLogic.Initialize(context, 1);

            Assert.That(PositionSwitchLogic.Release(context), Is.True);
            Assert.That(context.State.position, Is.Zero);
        }

        [Test]
        public void ReleaseDoesNotMoveSwitchWithoutSpringReturn()
        {
            var context = new PositionSwitchContext();
            PositionSwitchLogic.Configure(context, 0, 1, false, 0);
            PositionSwitchLogic.Initialize(context, 1);

            Assert.That(PositionSwitchLogic.Release(context), Is.False);
            Assert.That(context.State.position, Is.EqualTo(1));
        }

        [TestCase(-1, true)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        public void ContactCanCloseAtMultiplePositions(int position, bool expected)
        {
            bool actual = PositionSwitchContact.IsClosedAtPosition(
                position,
                new[] { -1, 1 });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ContactOutputCanBeInverted()
        {
            bool actual = PositionSwitchContact.IsClosedAtPosition(
                0,
                new[] { 0 },
                true);

            Assert.That(actual, Is.False);
        }
    }
}
