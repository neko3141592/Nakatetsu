using Nakatetsu.Train.Equipment.SpeedMeasurement;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Train.Tests
{
    public sealed class SpeedSensorTests
    {
        [TestCase(0f, 0f)]
        [TestCase(12.5f, 12.5f)]
        [TestCase(-12.5f, 12.5f)]
        public void MeasuresAbsolutePhysicalSpeed(float input, float expected)
        {
            var context = new SpeedSensorContext();
            context.Input.hasPhysicalSpeed = true;
            context.Input.signedPhysicalSpeedMps = input;
            SpeedSensorLogic.Calculate(context);
            Assert.That(context.Output.hasMeasurement, Is.True);
            Assert.That(context.Output.measuredSpeedMps, Is.EqualTo(expected));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPhysicalSpeedIsNotAZeroMeasurement(float input)
        {
            var context = new SpeedSensorContext();
            context.Input.hasPhysicalSpeed = true;
            context.Input.signedPhysicalSpeedMps = input;
            SpeedSensorLogic.Calculate(context);
            Assert.That(context.Output.hasMeasurement, Is.False);
        }

        [Test]
        public void RequiresNewPhysicalInputAndInvalidatesOnDisable()
        {
            var go = new GameObject("SpeedSensor");
            try
            {
                var sensor = go.AddComponent<SpeedSensor>();
                Assert.That(sensor.TryGetMeasuredSpeedMps(out _), Is.False);
                sensor.SetPhysicalSpeedMps(0f);
                sensor.CollectInput();
                sensor.Calculate(0.1f);
                Assert.That(sensor.TryGetMeasuredSpeedMps(out float speed), Is.True);
                Assert.That(speed, Is.Zero);
                sensor.CollectInput();
                sensor.Calculate(0.1f);
                Assert.That(sensor.TryGetMeasuredSpeedMps(out _), Is.False);
                sensor.SetPhysicalSpeedMps(10f);
                sensor.CollectInput();
                sensor.Calculate(0.1f);
                sensor.enabled = false;
                Assert.That(sensor.TryGetMeasuredSpeedMps(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
