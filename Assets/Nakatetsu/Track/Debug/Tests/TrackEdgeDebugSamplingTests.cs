using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Debugging.Tests
{
    public sealed class TrackEdgeDebugSamplingTests
    {
        [TestCase(20f, 1)]
        [TestCase(100f, 3)]
        [TestCase(99.99999f, 3)]
        [TestCase(120f, 3)]
        public void LabelsStartAtZeroAndUseFiftyMetreIntervals(float length, int expectedLabels)
        {
            var graph = Create(length, false, 0f, 0f, 0f, out var edge);
            Assert.That(TrackEdgeDebugSampling.TrySample(graph, edge, 7f, out var samples), Is.True);
            Assert.That(samples.Labels.Length, Is.EqualTo(expectedLabels));
            for (int i = 0; i < expectedLabels; i++)
            {
                Assert.That(samples.Labels[i].DistanceM, Is.EqualTo(i * 50f));
                Assert.That(samples.Labels[i].Position.z, Is.EqualTo(Mathf.Min(i * 50f, length)).Within(0.001f));
            }
            Assert.That(samples.LinePositions[0], Is.EqualTo(Vector3.zero));
            Assert.That(samples.LinePositions[^1].z, Is.EqualTo(length));
            Assert.That(samples.Labels[0].Text, Is.EqualTo("E  0 m"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurvedOffsetAndGradedEdgeUsesActualEdgeDistanceInNodeAToBDirection(bool reverse)
        {
            const float radius = 200f, offset = 20f, gradient = 0.1f;
            var graph = Create(200f, reverse, radius, offset, gradient, out var edge);
            Assert.That(TrackEdgeDebugSampling.TrySample(graph, edge, 5f, out var samples), Is.True);
            float rate = Mathf.Sqrt(0.9f * 0.9f + gradient * gradient);
            float geometryDistance = reverse ? 200f - 50f / rate : 50f / rate;
            float theta = geometryDistance / radius;
            Vector3 expected = new Vector3(radius * (1f - Mathf.Cos(theta)), geometryDistance * gradient,
                radius * Mathf.Sin(theta)) + offset * new Vector3(Mathf.Cos(theta), 0f, -Mathf.Sin(theta));
            Assert.That(Vector3.Distance(samples.Labels[1].Position, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void MissingDistanceTableDoesNotProduceAMisleadingStraightLine()
        {
            var graph = Create(120f, false, 0f, 0f, 0f, out var edge);
            edge.distanceMap.Clear();
            Assert.That(TrackEdgeDebugSampling.TrySample(graph, edge, 5f, out var samples), Is.False);
            Assert.That(samples, Is.Null);
        }

        private static TrackGraphContext Create(float length, bool reverse, float radius, float offset,
            float gradient, out TrackEdgeDefinition edge)
        {
            var definition = new TrackGraphDefinition();
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = length };
            geometry.horizontalSegments.Add(radius == 0f
                ? (TrackGeometryHorizontalSegment)new TrackGeometryStraightSegment { lengthM = length }
                : new TrackGeometryCircularSegment { radiusM = radius, lengthM = length });
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment { gradientPermille = gradient * 1000f, lengthM = length });
            definition.geometries.Add(geometry);
            edge = new TrackEdgeDefinition
            {
                edgeId = "E", geometryId = "G",
                startDistanceOnGeometryM = reverse ? length : 0f,
                endDistanceOnGeometryM = reverse ? 0f : length
            };
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = length, offsetM = offset });
            float horizontalRate = radius == 0f ? 1f : 1f - offset / radius;
            float edgeLength = length * Mathf.Sqrt(horizontalRate * horizontalRate + gradient * gradient);
            edge.distanceMap.Add(new TrackEdgeDistanceSample
            { distanceOnEdgeM = 0f, distanceOnGeometryM = edge.startDistanceOnGeometryM });
            edge.distanceMap.Add(new TrackEdgeDistanceSample
            { distanceOnEdgeM = edgeLength, distanceOnGeometryM = edge.endDistanceOnGeometryM });
            definition.edges.Add(edge);
            var graph = new TrackGraphContext();
            Assert.That(graph.TryBuildLookups(definition, out _), Is.True);
            return graph;
        }
    }
}
