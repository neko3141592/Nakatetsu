using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometrySecondDerivativeTests
    {
        private static IEnumerable<TrackGeometryHorizontalSegment> HorizontalShapes()
        {
            yield return new TrackGeometryStraightSegment();
            foreach (float radius in new[] { 500f, -500f })
            {
                yield return new TrackGeometryCircularSegment { radiusM = radius };
                yield return new TrackGeometryTransitionInSegment { radiusM = radius };
                yield return new TrackGeometryTransitionOutSegment { radiusM = radius };
            }
        }

        [TestCaseSource(nameof(HorizontalShapes))]
        public void HorizontalSecondDerivativeMatchesDerivativeDifference(TrackGeometryHorizontalSegment segment)
        {
            segment.startDistanceM = 80f;
            const float step = 0.125f;
            foreach (float local in new[] { 10f, 50f, 90f })
            {
                float distance = 80f + local;
                Vector3 numerical = (segment.EvaluateDerivative(distance + step)
                    - segment.EvaluateDerivative(distance - step)) / (2f * step);
                Assert.That(Vector3.Distance(segment.EvaluateSecondDerivative(distance), numerical), Is.LessThan(0.000001f));
            }
        }

        [TestCase(500f)]
        [TestCase(-500f)]
        public void HorizontalEndpointsReturnSegmentSideValues(float radius)
        {
            var incoming = new TrackGeometryTransitionInSegment { startDistanceM = 80f, radiusM = radius };
            var outgoing = new TrackGeometryTransitionOutSegment { startDistanceM = 80f, radiusM = radius };
            var circular = new TrackGeometryCircularSegment { startDistanceM = 80f, radiusM = radius };
            Assert.That(incoming.EvaluateSecondDerivative(80f), Is.EqualTo(Vector3.zero));
            Assert.That(incoming.EvaluateSecondDerivative(180f).x, Is.EqualTo(1f / radius).Within(1e-7f));
            Assert.That(outgoing.EvaluateSecondDerivative(80f).x, Is.EqualTo(1f / radius).Within(1e-7f));
            Assert.That(outgoing.EvaluateSecondDerivative(180f), Is.EqualTo(Vector3.zero));
            Assert.That(circular.EvaluateSecondDerivative(80f), Is.EqualTo(new Vector3(1f / radius, 0f, 0f)));
        }

        [TestCase(0f)]
        [TestCase(0.0005f)]
        [TestCase(-0.0005f)]
        public void TinyRadiusRetainsStraightFallback(float radius)
        {
            TrackGeometryHorizontalSegment[] shapes =
            {
                new TrackGeometryCircularSegment { radiusM = radius },
                new TrackGeometryTransitionInSegment { radiusM = radius },
                new TrackGeometryTransitionOutSegment { radiusM = radius }
            };
            foreach (var segment in shapes)
                Assert.That(segment.EvaluateSecondDerivative(25f), Is.EqualTo(Vector3.zero));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(0.0005f)]
        public void DegenerateTransitionLengthRetainsStraightFallback(float length)
        {
            Assert.That(new TrackGeometryTransitionInSegment { lengthM = length }.EvaluateSecondDerivative(0f), Is.EqualTo(Vector3.zero));
            Assert.That(new TrackGeometryTransitionOutSegment { lengthM = length }.EvaluateSecondDerivative(0f), Is.EqualTo(Vector3.zero));
        }

        [TestCase(0f, 20f)]
        [TestCase(20f, -20f)]
        [TestCase(20f, 20f)]
        public void LinearGradientReturnsSlopeChangePerMetre(float start, float end)
        {
            TrackGeometryVerticalSegment segment = new TrackGeometryLinearGradientSegment
            { startDistanceM = 80f, lengthM = 100f, startGradientPermille = start, endGradientPermille = end };
            foreach (float distance in new[] { 80f, 130f, 180f })
                Assert.That(segment.EvaluateSecondDerivative(distance), Is.EqualTo((end - start) / 100000f).Within(1e-9f));
            float numerical = (segment.EvaluateDerivative(130.125f) - segment.EvaluateDerivative(129.875f)) / 0.25f;
            Assert.That(segment.EvaluateSecondDerivative(130f), Is.EqualTo(numerical).Within(1e-7f));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(0.0005f)]
        [TestCase(0.001f)]
        public void DegenerateVerticalLengthReturnsZero(float length)
        {
            TrackGeometryVerticalSegment segment = new TrackGeometryLinearGradientSegment
            { lengthM = length, endGradientPermille = 20f };
            Assert.That(segment.EvaluateSecondDerivative(0f), Is.Zero);
        }

        [TestCase(-20f)]
        [TestCase(0f)]
        [TestCase(20f)]
        public void ConstantGradientReturnsZero(float gradient)
        {
            TrackGeometryVerticalSegment segment = new TrackGeometryConstantGradientSegment
            { startDistanceM = 80f, gradientPermille = gradient };
            foreach (float distance in new[] { 80f, 130f, 180f })
                Assert.That(segment.EvaluateSecondDerivative(distance), Is.Zero);
        }
    }
}
