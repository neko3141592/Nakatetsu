using NUnit.Framework;
using Nakatetsu.Track.Graph.Geometry;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeCalculatorTests
    {
        private static TrackGraphContext Create(bool reverse, float radius, float offset, out TrackEdgeDefinition edge)
        {
            var definition = new TrackGraphDefinition();
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = 100f };
            geometry.horizontalSegments.Add(radius == 0f
                ? (TrackGeometryHorizontalSegment)new TrackGeometryStraightSegment()
                : new TrackGeometryCircularSegment { radiusM = radius });
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment { gradientPermille = 20f });
            definition.geometries.Add(geometry);
            edge = new TrackEdgeDefinition
            {
                edgeId = "E", geometryId = "G",
                startDistanceOnGeometryM = reverse ? 100f : 0f,
                endDistanceOnGeometryM = reverse ? 0f : 100f
            };
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 100f, offsetM = offset });
            float horizontalRate = radius == 0f ? 1f : 1f - offset / radius;
            float length = 100f * Mathf.Sqrt(horizontalRate * horizontalRate + 0.02f * 0.02f);
            edge.distanceMap.Add(new TrackEdgeDistanceSample
            { distanceOnEdgeM = 0f, distanceOnGeometryM = edge.startDistanceOnGeometryM });
            edge.distanceMap.Add(new TrackEdgeDistanceSample
            { distanceOnEdgeM = length, distanceOnGeometryM = edge.endDistanceOnGeometryM });
            definition.edges.Add(edge);
            var context = new TrackGraphContext();
            Assert.That(context.TryBuildLookups(definition, out _), Is.True);
            return context;
        }

        [TestCase(false, 0f, 3f)]
        [TestCase(true, 0f, -3f)]
        [TestCase(false, 500f, 5f)]
        [TestCase(true, 500f, 5f)]
        [TestCase(false, -500f, 5f)]
        public void ReturnsPositionTangentRotationAndActualGradient(bool reverse, float radius, float offset)
        {
            var graph = Create(reverse, radius, offset, out var edge);
            float distance = edge.distanceMap[1].distanceOnEdgeM / 2f;
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, "E", distance, out var sample), Is.True);
            float theta = radius == 0f ? 0f : 50f / radius;
            Vector3 basePosition = radius == 0f ? new Vector3(0f, 1f, 50f)
                : new Vector3(radius * (1f - Mathf.Cos(theta)), 1f, radius * Mathf.Sin(theta));
            Vector3 right = new Vector3(Mathf.Cos(theta), 0f, -Mathf.Sin(theta));
            float rate = radius == 0f ? 1f : 1f - offset / radius;
            Vector3 expectedDerivative = new Vector3(rate * Mathf.Sin(theta), 0.02f, rate * Mathf.Cos(theta));
            if (reverse) expectedDerivative = -expectedDerivative;
            Assert.That(sample.DistanceM, Is.EqualTo(distance));
            Assert.That(Vector3.Distance(sample.Position, basePosition + offset * right), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(sample.Tangent, expectedDerivative.normalized), Is.LessThan(1e-6f));
            Assert.That(Vector3.Distance(sample.Rotation * Vector3.forward, sample.Tangent), Is.LessThan(1e-6f));
            Assert.That(sample.GradientPermille, Is.EqualTo((reverse ? -20f : 20f) / rate).Within(1e-4f));
        }

        [Test]
        public void OffsetDerivativeTiltsHeading()
        {
            var graph = Create(false, 0f, 0f, out var edge);
            edge.offsetSegments[0] = new LinearTestOffset { endDistanceOnGeometryM = 100f };
            float length = 100f * Mathf.Sqrt(1f + 0.1f * 0.1f + 0.02f * 0.02f);
            edge.distanceMap[1] = new TrackEdgeDistanceSample { distanceOnGeometryM = 100f, distanceOnEdgeM = length };
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, "E", length / 2f, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(5f, 1f, 50f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(sample.Tangent, new Vector3(0.1f, 0.02f, 1f).normalized), Is.LessThan(1e-6f));
        }

        [Test]
        public void InvalidInputAndCollapsedHorizontalPathFailWithDefaultSample()
        {
            Assert.That(TrackEdgeCalculator.TryEvaluate(null, "E", 0f, out var sample), Is.False);
            Assert.That(sample, Is.EqualTo(default(TrackSample)));
            var graph = Create(false, 500f, 500f, out _);
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, "E", 0f, out sample), Is.False);
            Assert.That(sample, Is.EqualTo(default(TrackSample)));
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, "missing", 0f, out _), Is.False);
            Assert.That(TrackEdgeCalculator.TryEvaluate(graph, "E", float.NaN, out _), Is.False);
        }

        private sealed class LinearTestOffset : TrackEdgeOffsetSegment
        {
            public override float EvaluateOffsetM(float distanceOnGeometryM) => distanceOnGeometryM * 0.1f;
            public override float EvaluateDerivative(float distanceOnGeometryM) => 0.1f;
        }
    }
}
