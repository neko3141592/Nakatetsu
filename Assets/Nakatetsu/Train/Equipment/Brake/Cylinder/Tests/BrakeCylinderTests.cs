using NUnit.Framework;
using Nakatetsu.Train.Equipment.Brake.Cylinder;

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
    }
}
