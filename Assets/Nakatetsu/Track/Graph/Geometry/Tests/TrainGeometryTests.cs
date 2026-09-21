using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Track.TrainGeometry;

namespace Nakatetsu.Track.Tests
{
    public sealed class TrainGeometryTests
    {
        private static TrainGeometryDefinition Straight()
        {
            return new TrainGeometryDefinition
            {
                lengthM = 100f,
                horizontalSegments = new List<TrainGeometryHorizontalSegment>
                {
                    new TrainGeometryHorizontalSegment { lengthM = 100f }
                }
            };
        }

        [TestCase(-10f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(25f, 25f)]
        [TestCase(100f, 100f)]
        [TestCase(120f, 100f)]
        public void StraightClampsDistance(float distance, float expected)
        {
            Assert.That(TrainGeometryCalculator.TryEvaluate(Straight(), distance, out var sample), Is.True);
            Assert.That(sample.DistanceM, Is.EqualTo(expected));
            Assert.That(Vector3.Distance(sample.Position, new Vector3(0, 0, expected)), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(sample.Rotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void OriginAndYawAreApplied()
        {
            var line = Straight();
            line.originPosition = new Vector3(10, 20, 30);
            line.originRotation = Quaternion.Euler(0, 90, 0);
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, 25, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(35, 20, 30)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(sample.Tangent, Vector3.right), Is.LessThan(0.001f));
        }

        [TestCase(100f, 100f)]
        [TestCase(-100f, -100f)]
        public void QuarterCircleUsesSignedRadius(float radius, float x)
        {
            var line = Straight();
            line.lengthM = Mathf.PI * 50f;
            line.horizontalSegments[0] = new TrainGeometryHorizontalSegment
            {
                lengthM = line.lengthM, trackCurveType = TrainGeometryCurveType.Curve, radiusM = radius
            };
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, line.lengthM, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(x, 0, 100)), Is.LessThan(0.001f));
        }

        [Test]
        public void StraightThenCurveAccumulatesFromPreviousEndpoint()
        {
            var line = Straight();
            line.lengthM = 20 + Mathf.PI * 50;
            line.horizontalSegments[0].lengthM = 20;
            line.horizontalSegments.Add(new TrainGeometryHorizontalSegment
            {
                startDistanceM = 20, lengthM = Mathf.PI * 50,
                trackCurveType = TrainGeometryCurveType.Curve, radiusM = 100
            });
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, line.lengthM, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(100, 0, 120)), Is.LessThan(0.001f));
        }

        [Test]
        public void GradientProducesPitchWithoutRoll()
        {
            var line = Straight();
            line.verticalSegments.Add(new TrainGeometryVerticalSegment
            { lengthM = 100, startGradientPermille = 20, endGradientPermille = 20 });
            line.originRotation = Quaternion.Euler(0, 0, 30);
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, 50, out var sample), Is.True);
            Assert.That(sample.Position.y, Is.EqualTo(1).Within(0.0001));
            Assert.That(sample.GradientPermille, Is.EqualTo(20));
            Assert.That(sample.Tangent.y, Is.GreaterThan(0));
            var expected = Quaternion.Euler(-Mathf.Atan(.02f) * Mathf.Rad2Deg, 0, 0);
            Assert.That(Quaternion.Angle(sample.Rotation, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void GradientRampIntegratesHeightAndExtendsAcrossGap()
        {
            var line = Straight();
            line.verticalSegments.Add(new TrainGeometryVerticalSegment
            { lengthM = 50, startGradientPermille = 0, endGradientPermille = 20 });
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, 75, out var sample), Is.True);
            Assert.That(sample.Position.y, Is.EqualTo(1).Within(0.0001));
            Assert.That(sample.GradientPermille, Is.EqualTo(20));
        }

        [TestCase(TrainGeometryCurveType.TransitionIn, 0.41666667f)]
        [TestCase(TrainGeometryCurveType.TransitionOut, 2.08333333f)]
        public void TransitionRetainsLegacyCubicApproximation(TrainGeometryCurveType type, float expectedX)
        {
            var line = Straight();
            line.horizontalSegments[0].trackCurveType = type;
            line.horizontalSegments[0].radiusM = 500;
            Assert.That(TrainGeometryCalculator.TryEvaluate(line, 50, out var sample), Is.True);
            // x=in: 50^3/(6*100*500); out: 50^2/(2*500)-in.
            Assert.That(sample.Position.x, Is.EqualTo(expectedX).Within(0.0001));
            Assert.That(sample.Position.z, Is.EqualTo(50));
        }

        [Test]
        public void InvalidInputsDoNotProduceAPose()
        {
            Assert.That(TrainGeometryCalculator.TryEvaluate(null, 0, out _), Is.False);
            Assert.That(TrainGeometryCalculator.TryEvaluate(new TrainGeometryDefinition(), 0, out _), Is.False);
            Assert.That(TrainGeometryCalculator.TryEvaluate(Straight(), float.NaN, out _), Is.False);
            Assert.That(TrainGeometryCalculator.TryEvaluate(Straight(), float.PositiveInfinity, out _), Is.False);
        }

        [Test]
        public void EvaluationDoesNotMutateDefinition()
        {
            var line = Straight();
            string before = JsonUtility.ToJson(line);
            TrainGeometryCalculator.TryEvaluate(line, 75, out _);
            Assert.That(JsonUtility.ToJson(line), Is.EqualTo(before));
        }
    }
}
