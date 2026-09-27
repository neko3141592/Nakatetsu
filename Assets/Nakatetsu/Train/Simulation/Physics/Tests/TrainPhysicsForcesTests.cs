using Nakatetsu.Train.Simulation.Physics;
using NUnit.Framework;

namespace Nakatetsu.Train.Tests
{
    public sealed class TrainPhysicsForcesTests
    {
        [Test]
        public void RunningResistanceOpposesBothDirections()
        {
            var input = new TrainPhysicsInput
            {
                totalRunningResistanceAN = 300f,
                totalRunningResistanceBNsPerM = 8f,
                totalRunningResistanceCNs2PerM2 = 0.5f
            };

            Assert.That(TrainPhysicsLogic.CalculateRunningResistanceForceN(input, 10f),
                Is.EqualTo(-430f).Within(0.001f));
            Assert.That(TrainPhysicsLogic.CalculateRunningResistanceForceN(input, -10f),
                Is.EqualTo(430f).Within(0.001f));
        }

        [Test]
        public void GradeForcePointsDownhillRegardlessOfTravelDirection()
        {
            float uphillForceN = TrainPhysicsLogic.CalculateGradeForceN(1000f, 10f);
            float downhillForceN = TrainPhysicsLogic.CalculateGradeForceN(1000f, -10f);

            Assert.That(uphillForceN, Is.LessThan(0f));
            Assert.That(downhillForceN, Is.EqualTo(-uphillForceN).Within(0.001f));
        }

        [Test]
        public void GradeCanReverseTrainAfterForwardMotionStops()
        {
            var context = new TrainPhysicsContext();
            context.Input.totalMassKg = 1000f;
            context.Input.totalGradeForceN = -1000f;
            context.Input.totalRunningResistanceAN = 300f;
            context.State.signedVelocityMps = 1f;

            TrainPhysicsLogic.Calculate(context, 2f);
            Assert.That(context.State.signedVelocityMps, Is.Zero);
            Assert.That(context.State.signedDisplacementM, Is.GreaterThan(0f));

            TrainPhysicsLogic.Calculate(context, 1f);
            Assert.That(context.State.signedVelocityMps, Is.EqualTo(-0.7f).Within(0.001f));
            Assert.That(context.State.signedDisplacementM, Is.LessThan(0f));
        }

        [Test]
        public void ResistanceDoesNotMoveStoppedTrainOrCancelSteeperGrade()
        {
            var context = new TrainPhysicsContext();
            context.Input.totalMassKg = 1000f;
            context.Input.totalRunningResistanceAN = 300f;
            context.Input.totalGradeForceN = -200f;

            TrainPhysicsLogic.Calculate(context, 1f);
            Assert.That(context.State.signedVelocityMps, Is.Zero);

            context.Input.totalGradeForceN = -400f;
            TrainPhysicsLogic.Calculate(context, 1f);
            Assert.That(context.State.signedVelocityMps, Is.EqualTo(-0.1f).Within(0.001f));
        }

        [Test]
        public void BrakeHoldsTrainUntilGradeExceedsBrakeAndResistance()
        {
            var context = new TrainPhysicsContext();
            context.Input.totalMassKg = 1000f;
            context.Input.totalRunningResistanceAN = 300f;
            context.Input.totalGradeForceN = -1000f;
            context.Input.totalBrakeForceN = 800f;

            TrainPhysicsLogic.Calculate(context, 1f);
            Assert.That(context.State.signedVelocityMps, Is.Zero);

            context.Input.totalGradeForceN = -1200f;
            TrainPhysicsLogic.Calculate(context, 1f);
            Assert.That(context.State.signedVelocityMps, Is.EqualTo(-0.1f).Within(0.001f));
        }
    }
}
