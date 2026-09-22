using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Track.Graph.Geometry;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometryTests
    {
        private static TrackGeometryDefinition Straight()
        {
            return new TrackGeometryDefinition
            {
                lengthM = 100f,
                horizontalSegments = new List<TrackGeometryHorizontalSegment>
                {
                    new TrackGeometryStraightSegment { lengthM = 100f }
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
            Assert.That(TrackGeometryCalculator.TryEvaluate(Straight(), distance, out var sample), Is.True);
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
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, 25, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(35, 20, 30)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(sample.Tangent, Vector3.right), Is.LessThan(0.001f));
        }

        [TestCase(100f, 100f)]
        [TestCase(-100f, -100f)]
        public void QuarterCircleUsesSignedRadius(float radius, float x)
        {
            var line = Straight();
            line.lengthM = Mathf.PI * 50f;
            line.horizontalSegments[0] = new TrackGeometryCircularSegment
            {
                lengthM = line.lengthM, radiusM = radius
            };
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, line.lengthM, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(x, 0, 100)), Is.LessThan(0.001f));
        }

        [Test]
        public void StraightThenCurveAccumulatesFromPreviousEndpoint()
        {
            var line = Straight();
            line.lengthM = 20 + Mathf.PI * 50;
            line.horizontalSegments[0].lengthM = 20;
            line.horizontalSegments.Add(new TrackGeometryCircularSegment
            {
                startDistanceM = 20, lengthM = Mathf.PI * 50,
                radiusM = 100
            });
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, line.lengthM, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(100, 0, 120)), Is.LessThan(0.001f));
        }

        [Test]
        public void GradientProducesPitchWithoutRoll()
        {
            var line = Straight();
            line.verticalSegments.Add(new TrackGeometryLinearGradientSegment
            { lengthM = 100, startGradientPermille = 20, endGradientPermille = 20 });
            line.originRotation = Quaternion.Euler(0, 0, 30);
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, 50, out var sample), Is.True);
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
            line.verticalSegments.Add(new TrackGeometryLinearGradientSegment
            { lengthM = 50, startGradientPermille = 0, endGradientPermille = 20 });
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, 75, out var sample), Is.True);
            Assert.That(sample.Position.y, Is.EqualTo(1).Within(0.0001));
            Assert.That(sample.GradientPermille, Is.EqualTo(20));
        }

        [TestCase(TrackGeometryCurveType.TransitionIn, 0.41666667f)]
        [TestCase(TrackGeometryCurveType.TransitionOut, 2.08333333f)]
        public void TransitionRetainsLegacyCubicApproximation(TrackGeometryCurveType type, float expectedX)
        {
            var line = Straight();
            line.horizontalSegments[0] = type == TrackGeometryCurveType.TransitionIn
                ? new TrackGeometryTransitionInSegment { lengthM = 100f, radiusM = 500f }
                : new TrackGeometryTransitionOutSegment { lengthM = 100f, radiusM = 500f };
            Assert.That(TrackGeometryCalculator.TryEvaluate(line, 50, out var sample), Is.True);
            // x=in: 50^3/(6*100*500); out: 50^2/(2*500)-in.
            Assert.That(sample.Position.x, Is.EqualTo(expectedX).Within(0.0001));
            Assert.That(sample.Position.z, Is.EqualTo(50));
        }

        [Test]
        public void InvalidInputsDoNotProduceAPose()
        {
            Assert.That(TrackGeometryCalculator.TryEvaluate(null, 0, out _), Is.False);
            Assert.That(TrackGeometryCalculator.TryEvaluate(new TrackGeometryDefinition(), 0, out _), Is.False);
            Assert.That(TrackGeometryCalculator.TryEvaluate(Straight(), float.NaN, out _), Is.False);
            Assert.That(TrackGeometryCalculator.TryEvaluate(Straight(), float.PositiveInfinity, out _), Is.False);
        }

        [Test]
        public void EvaluationDoesNotMutateDefinition()
        {
            var line = Straight();
            string before = JsonUtility.ToJson(line);
            TrackGeometryCalculator.TryEvaluate(line, 75, out _);
            Assert.That(JsonUtility.ToJson(line), Is.EqualTo(before));
        }
    }
}
