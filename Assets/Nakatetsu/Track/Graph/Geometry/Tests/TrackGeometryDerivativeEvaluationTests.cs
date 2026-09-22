using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometryDerivativeEvaluationTests
    {
        private static IEnumerable<TrackGeometryHorizontalSegment> Shapes()
        {
            yield return new TrackGeometryStraightSegment();
            yield return new TrackGeometryCircularSegment { radiusM = 500f };
            yield return new TrackGeometryCircularSegment { radiusM = -500f };
            yield return new TrackGeometryTransitionInSegment { radiusM = 500f };
            yield return new TrackGeometryTransitionOutSegment { radiusM = -500f };
        }

        private static TrackGeometryDefinition Create(TrackGeometryHorizontalSegment segment)
        {
            var geometry = new TrackGeometryDefinition
            {
                lengthM = 100f, originPosition = new Vector3(10f, 20f, 30f),
                originRotation = Quaternion.Euler(0f, 45f, 0f)
            };
            geometry.horizontalSegments.Add(segment);
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment { gradientPermille = 20f });
            return geometry;
        }

        [TestCaseSource(nameof(Shapes))]
        public void ReturnsWorldPositionAndUnnormalizedDerivativeWithoutMutation(TrackGeometryHorizontalSegment segment)
        {
            var geometry = Create(segment);
            string before = JsonUtility.ToJson(geometry);
            foreach (float distance in new[] { 0f, 50f, 100f })
            {
                Assert.That(geometry.TryEvaluateAtGeometryDistance(distance, out var position, out var derivative), Is.True);
                Assert.That(TrackGeometryCalculator.TryEvaluate(geometry, distance, out var legacy), Is.True);
                Assert.That(Vector3.Distance(position, legacy.Position), Is.LessThan(0.0001f));
                Vector3 expected = geometry.originRotation * segment.EvaluateDerivative(distance);
                expected.y = 0.02f;
                Assert.That(Vector3.Distance(derivative, expected), Is.LessThan(0.000001f));
            }
            Assert.That(JsonUtility.ToJson(geometry), Is.EqualTo(before));
        }

        [TestCaseSource(nameof(Shapes))]
        public void WorldDerivativeMatchesPositionDifference(TrackGeometryHorizontalSegment segment)
        {
            var geometry = Create(segment);
            geometry.verticalSegments[0] = new TrackGeometryLinearGradientSegment
            { startGradientPermille = -10f, endGradientPermille = 30f };
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out _, out var derivative), Is.True);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50.5f, out var after, out _), Is.True);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(49.5f, out var before, out _), Is.True);
            Assert.That(Vector3.Distance(derivative, after - before), Is.LessThan(0.0001f));
        }

        [Test]
        public void RotatesDerivativeBySegmentStartFrameAndUsesEarlierSideAtSharedEndpoint()
        {
            var geometry = Create(new TrackGeometryTransitionInSegment { radiusM = 100f });
            geometry.lengthM = 200f;
            geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { startDistanceM = 100f });
            Assert.That(geometry.TryEvaluateAtGeometryDistance(100f, out _, out var atBoundary), Is.True);
            Vector3 expectedBoundary = geometry.originRotation * new Vector3(0.5f, 0f, 1f);
            expectedBoundary.y = 0.02f;
            Assert.That(Vector3.Distance(atBoundary, expectedBoundary), Is.LessThan(0.000001f));
            Assert.That(geometry.TryEvaluateAtGeometryDistance(150f, out var position, out var derivative), Is.True);
            Vector3 expected = geometry.originRotation * Quaternion.Euler(0f, 0.5f * Mathf.Rad2Deg, 0f) * Vector3.forward;
            expected.y = 0.02f;
            Assert.That(Vector3.Distance(derivative, expected), Is.LessThan(0.000001f));
            Assert.That(TrackGeometryCalculator.TryEvaluate(geometry, 150f, out var legacy), Is.True);
            Assert.That(Vector3.Distance(position, legacy.Position), Is.LessThan(0.0001f));
        }

        [TestCase(10f, 0f, 0f)]
        [TestCase(30f, 0.2f, 0.02f)]
        [TestCase(50f, 0.6f, 0.02f)]
        [TestCase(70f, 0.9f, 0f)]
        [TestCase(90f, 0.6f, -0.02f)]
        public void CombinesVerticalSegmentsGapsAndTail(float distance, float height, float slope)
        {
            var geometry = Create(new TrackGeometryStraightSegment());
            geometry.verticalSegments.Clear();
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment
            { startDistanceM = 20f, lengthM = 20f, gradientPermille = 20f });
            geometry.verticalSegments.Add(new TrackGeometryLinearGradientSegment
            { startDistanceM = 60f, lengthM = 20f, startGradientPermille = 20f, endGradientPermille = -20f });
            Assert.That(geometry.TryEvaluateAtGeometryDistance(distance, out var position, out var derivative), Is.True);
            Assert.That(position.y, Is.EqualTo(20f + height).Within(0.00001f));
            Assert.That(derivative.y, Is.EqualTo(slope).Within(0.000001f));
        }

        [TestCase(-1f)]
        [TestCase(101f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDistanceFailsWithoutClamping(float distance)
        {
            Assert.That(Create(new TrackGeometryStraightSegment()).TryEvaluateAtGeometryDistance(distance,
                out var position, out var derivative), Is.False);
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(derivative, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvalidDefinitionsAndNonfiniteDerivativeFailWithoutPartialOutputs()
        {
            var geometry = Create(new InvalidDerivativeSegment());
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out var position, out var derivative), Is.False);
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(derivative, Is.EqualTo(Vector3.zero));
            geometry.horizontalSegments[0] = new TrackGeometryStraightSegment { startDistanceM = 10f };
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out _, out _), Is.False);
            geometry.horizontalSegments.Clear();
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out _, out _), Is.False);
            Assert.That(TrackGeometryCalculator.TryEvaluateAtGeometryDistance(null, 0f, out _, out _), Is.False);
        }

        [TestCaseSource(nameof(Shapes))]
        public void CombinedSecondDerivativeMatchesWorldFirstDerivativeDifference(TrackGeometryHorizontalSegment segment)
        {
            var geometry = Create(segment);
            geometry.verticalSegments[0] = new TrackGeometryLinearGradientSegment
            { startGradientPermille = -10f, endGradientPermille = 30f };
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out var position, out var derivative, out var second), Is.True);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out var oldPosition, out var oldDerivative), Is.True);
            Assert.That(position, Is.EqualTo(oldPosition));
            Assert.That(derivative, Is.EqualTo(oldDerivative));
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50.125f, out _, out var after), Is.True);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(49.875f, out _, out var before), Is.True);
            Assert.That(Vector3.Distance(second, (after - before) / 0.25f), Is.LessThan(0.000001f));
            Vector3 expected = geometry.originRotation * segment.EvaluateSecondDerivative(50f);
            expected.y = 0.0004f;
            Assert.That(Vector3.Distance(second, expected), Is.LessThan(0.000001f));
        }

        [TestCase(10f, 0f)]
        [TestCase(20f, 0.001f)]
        [TestCase(40f, 0.001f)]
        [TestCase(50f, 0f)]
        [TestCase(60f, -0.002f)]
        [TestCase(80f, -0.002f)]
        [TestCase(90f, 0f)]
        public void VerticalSecondDerivativeUsesEndpointAndGapRules(float distance, float expected)
        {
            var geometry = Create(new TrackGeometryStraightSegment());
            geometry.verticalSegments.Clear();
            geometry.verticalSegments.Add(new TrackGeometryLinearGradientSegment
            { startDistanceM = 20f, lengthM = 20f, endGradientPermille = 20f });
            geometry.verticalSegments.Add(new TrackGeometryLinearGradientSegment
            { startDistanceM = 60f, lengthM = 20f, startGradientPermille = 20f, endGradientPermille = -20f });
            Assert.That(geometry.TryEvaluateAtGeometryDistance(distance, out _, out _, out var second), Is.True);
            Assert.That(second.y, Is.EqualTo(expected).Within(1e-8f));
        }

        [Test]
        public void SecondDerivativeUsesAccumulatedSegmentFrameAndEarlierBoundarySide()
        {
            var geometry = Create(new TrackGeometryTransitionInSegment { radiusM = 100f });
            geometry.lengthM = 200f;
            var next = new TrackGeometryCircularSegment { startDistanceM = 100f, radiusM = -200f };
            geometry.horizontalSegments.Add(next);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(100f, out _, out _, out var boundary), Is.True);
            Assert.That(Vector3.Distance(boundary, geometry.originRotation * new Vector3(0.01f, 0f, 0f)), Is.LessThan(1e-7f));
            Assert.That(geometry.TryEvaluateAtGeometryDistance(150f, out _, out _, out var second), Is.True);
            Vector3 expected = geometry.originRotation * Quaternion.Euler(0f, 0.5f * Mathf.Rad2Deg, 0f)
                * next.EvaluateSecondDerivative(150f);
            Assert.That(Vector3.Distance(second, expected), Is.LessThan(1e-7f));
        }

        [Test]
        public void InvalidSecondDerivativeClearsAllOutputsWithoutBreakingFirstDerivativeOverload()
        {
            var geometry = Create(new InvalidSecondDerivativeSegment());
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out _, out _), Is.True);
            Assert.That(geometry.TryEvaluateAtGeometryDistance(50f, out var position, out var derivative, out var second), Is.False);
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(derivative, Is.EqualTo(Vector3.zero));
            Assert.That(second, Is.EqualTo(Vector3.zero));
            Assert.That(geometry.TryEvaluateAtGeometryDistance(-1f, out _, out _, out _), Is.False);
        }

        private sealed class InvalidSecondDerivativeSegment : TrackGeometryHorizontalSegment
        {
            public override void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees)
            {
                position = new Vector3(0f, 0f, distanceOnGeometryM);
                headingDegrees = 0f;
            }
            public override Vector3 EvaluateDerivative(float distanceOnGeometryM) => Vector3.forward;
            public override Vector3 EvaluateSecondDerivative(float distanceOnGeometryM) => new Vector3(float.NaN, 0f, 0f);
        }

        private sealed class InvalidDerivativeSegment : TrackGeometryHorizontalSegment
        {
            public override Vector3 EvaluateSecondDerivative(float distanceOnGeometryM) => Vector3.zero;
            public override void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees)
            {
                position = new Vector3(0f, 0f, distanceOnGeometryM);
                headingDegrees = 0f;
            }
            public override Vector3 EvaluateDerivative(float distanceOnGeometryM) => new Vector3(float.NaN, 0f, 1f);
        }
    }
}
