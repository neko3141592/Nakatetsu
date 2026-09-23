using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeDistanceMapTests
    {
        private static TrackEdgeDefinition CreateEdge(bool reverse = false)
        {
            return new TrackEdgeDefinition
            {
                distanceMap = new List<TrackEdgeDistanceSample>
                {
                    Sample(0f, reverse ? 100f : 20f),
                    Sample(10f, reverse ? 80f : 40f),
                    Sample(40f, reverse ? 20f : 100f)
                }
            };
        }

        private static TrackEdgeDistanceSample Sample(float edge, float geometry) =>
            new TrackEdgeDistanceSample { distanceOnEdgeM = edge, distanceOnGeometryM = geometry };

        [TestCase(false, 0f, 20f)]
        [TestCase(false, 5f, 30f)]
        [TestCase(false, 10f, 40f)]
        [TestCase(false, 25f, 70f)]
        [TestCase(false, 40f, 100f)]
        [TestCase(true, 0f, 100f)]
        [TestCase(true, 5f, 90f)]
        [TestCase(true, 10f, 80f)]
        [TestCase(true, 25f, 50f)]
        [TestCase(true, 40f, 20f)]
        public void InterpolatesBothDirectionsWithoutMutatingDefinition(bool reverse, float distance, float expected)
        {
            var edge = CreateEdge(reverse);
            string before = JsonUtility.ToJson(edge);
            Assert.That(edge.TryConvertToGeometryDistance(distance, out float actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected).Within(0.000001f));
            Assert.That(JsonUtility.ToJson(edge), Is.EqualTo(before));
        }

        [TestCase(-0.001f)]
        [TestCase(40.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidInputReturnsFalseWithoutClamping(float distance)
        {
            Assert.That(CreateEdge().TryConvertToGeometryDistance(distance, out float actual), Is.False);
            Assert.That(actual, Is.Zero);
        }

        [TestCase("null")]
        [TestCase("empty")]
        [TestCase("single")]
        [TestCase("nonzeroStart")]
        [TestCase("duplicate")]
        [TestCase("unordered")]
        [TestCase("nanGeometry")]
        [TestCase("infiniteEdge")]
        [TestCase("zeroLength")]
        public void InvalidMapDetectedDuringSearchReturnsFalse(string kind)
        {
            var edge = CreateEdge();
            switch (kind)
            {
                case "null": edge.distanceMap = null; break;
                case "empty": edge.distanceMap.Clear(); break;
                case "single": edge.distanceMap = new List<TrackEdgeDistanceSample> { Sample(0f, 20f) }; break;
                case "nonzeroStart": edge.distanceMap[0] = Sample(1f, 20f); break;
                case "duplicate": edge.distanceMap[1] = Sample(0f, 40f); break;
                case "unordered": edge.distanceMap[1] = Sample(50f, 40f); break;
                case "nanGeometry": edge.distanceMap[1] = Sample(10f, float.NaN); break;
                case "infiniteEdge": edge.distanceMap[2] = Sample(float.PositiveInfinity, 100f); break;
                case "zeroLength": edge.distanceMap[2] = Sample(0f, 100f); break;
            }
            Assert.That(edge.TryConvertToGeometryDistance(5f, out float actual), Is.False);
            Assert.That(actual, Is.Zero);
        }

        [Test]
        public void TwoPointMapInterpolatesWithoutFloatOverflow()
        {
            var edge = new TrackEdgeDefinition
            {
                distanceMap = new List<TrackEdgeDistanceSample>
                { Sample(0f, -float.MaxValue), Sample(100f, float.MaxValue) }
            };
            Assert.That(edge.TryConvertToGeometryDistance(50f, out float actual), Is.True);
            Assert.That(actual, Is.Zero);
        }

        [Test]
        public void ManyNonuniformSamplesSelectTheCorrectInterval()
        {
            var edge = new TrackEdgeDefinition();
            for (int i = 0; i <= 128; i++) edge.distanceMap.Add(Sample(i * i, 1000f - i));
            for (int i = 0; i < 128; i++)
            {
                float distance = (i * i + (i + 1) * (i + 1)) * 0.5f;
                Assert.That(edge.TryConvertToGeometryDistance(distance, out float actual), Is.True);
                Assert.That(actual, Is.EqualTo(1000f - i - 0.5f));
            }
        }
    }
}
