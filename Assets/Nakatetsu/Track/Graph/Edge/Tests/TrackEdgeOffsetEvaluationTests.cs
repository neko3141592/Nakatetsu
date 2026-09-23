using NUnit.Framework;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeOffsetEvaluationTests
    {
        private static TrackEdgeDefinition CreateEdge(bool reverse = false)
        {
            var edge = new TrackEdgeDefinition
            {
                startDistanceOnGeometryM = reverse ? 100f : 20f,
                endDistanceOnGeometryM = reverse ? 20f : 100f
            };
            edge.offsetSegments.Add(Constant(20f, 60f, -3f));
            edge.offsetSegments.Add(Constant(60f, 100f, 5f));
            return edge;
        }

        private static TrackEdgeConstantOffsetSegment Constant(float start, float end, float offset) =>
            new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = start, endDistanceOnGeometryM = end, offsetM = offset };

        [TestCase(20f, -3f)]
        [TestCase(40f, -3f)]
        [TestCase(60f, 5f)]
        [TestCase(80f, 5f)]
        [TestCase(100f, 5f)]
        public void SelectsSegmentIncludingBoundariesInEitherEdgeDirection(float distance, float expected)
        {
            foreach (bool reverse in new[] { false, true })
            {
                var edge = CreateEdge(reverse);
                string before = JsonUtility.ToJson(edge);
                Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(distance, out float offset), Is.True);
                Assert.That(offset, Is.EqualTo(expected));
                Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(distance, out float value, out float derivative), Is.True);
                Assert.That(value, Is.EqualTo(expected));
                Assert.That(derivative, Is.Zero);
                Assert.That(JsonUtility.ToJson(edge), Is.EqualTo(before));
                Assert.That(edge.distanceMap, Is.Empty);
            }
        }

        [TestCase(19.99f)]
        [TestCase(100.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidDistance(float distance)
        {
            Assert.That(CreateEdge().TryEvaluateOffsetAtGeometryDistance(distance, out float offset), Is.False);
            Assert.That(offset, Is.Zero);
        }

        [TestCase("nullList")]
        [TestCase("empty")]
        [TestCase("nullSegment")]
        [TestCase("gap")]
        [TestCase("overlap")]
        [TestCase("unsorted")]
        [TestCase("zeroLength")]
        [TestCase("reversedSegment")]
        [TestCase("nanRange")]
        [TestCase("infiniteOffset")]
        [TestCase("nanOffset")]
        [TestCase("invalidEdge")]
        public void MissingOrInvalidDefinitionReturnsFalse(string kind)
        {
            var edge = CreateEdge();
            switch (kind)
            {
                case "nullList": edge.offsetSegments = null; break;
                case "empty": edge.offsetSegments.Clear(); break;
                case "nullSegment": edge.offsetSegments[1] = null; break;
                case "gap": edge.offsetSegments[0].endDistanceOnGeometryM = 30f; break;
                case "overlap": edge.offsetSegments[1].startDistanceOnGeometryM = 50f; break;
                case "unsorted": edge.offsetSegments.Reverse(); break;
                case "zeroLength": edge.offsetSegments[0].endDistanceOnGeometryM = 20f; break;
                case "reversedSegment": edge.offsetSegments[0].endDistanceOnGeometryM = 10f; break;
                case "nanRange": edge.offsetSegments[1].endDistanceOnGeometryM = float.NaN; break;
                case "infiniteOffset": ((TrackEdgeConstantOffsetSegment)edge.offsetSegments[0]).offsetM = float.PositiveInfinity; break;
                case "nanOffset": ((TrackEdgeConstantOffsetSegment)edge.offsetSegments[0]).offsetM = float.NaN; break;
                case "invalidEdge": edge.endDistanceOnGeometryM = edge.startDistanceOnGeometryM; break;
            }
            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(40f, out float offset), Is.False);
            Assert.That(offset, Is.Zero);
        }

        [Test]
        public void PassesAbsoluteGeometryDistanceToSelectedShape()
        {
            var edge = CreateEdge();
            edge.offsetSegments[0] = new DistanceEchoSegment
            { startDistanceOnGeometryM = 20f, endDistanceOnGeometryM = 60f };
            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(40f, out float offset), Is.True);
            Assert.That(offset, Is.EqualTo(40f));
            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(40f, out offset, out float derivative), Is.True);
            Assert.That(offset, Is.EqualTo(40f));
            Assert.That(derivative, Is.EqualTo(1f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonfiniteDerivativeFailsWithoutPartialOutputs(float invalidDerivative)
        {
            var edge = CreateEdge();
            edge.offsetSegments[0] = new InvalidDerivativeSegment
            { startDistanceOnGeometryM = 20f, endDistanceOnGeometryM = 60f, derivative = invalidDerivative };
            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(40f, out float offset, out float derivative), Is.False);
            Assert.That(offset, Is.Zero);
            Assert.That(derivative, Is.Zero);
        }

        private sealed class InvalidDerivativeSegment : TrackEdgeOffsetSegment
        {
            public float derivative;
            public override float EvaluateOffsetM(float distanceOnGeometryM) => 3f;
            public override float EvaluateDerivative(float distanceOnGeometryM) => derivative;
        }

        private sealed class DistanceEchoSegment : TrackEdgeOffsetSegment
        {
            public override float EvaluateOffsetM(float distanceOnGeometryM) => distanceOnGeometryM;
            public override float EvaluateDerivative(float distanceOnGeometryM) => 1f;
        }
    }
}
