using NUnit.Framework;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeConstantOffsetSegmentTests
    {
        [TestCase(-3.5f)]
        [TestCase(0f)]
        [TestCase(3.5f)]
        public void ReturnsConstantOffsetAndZeroDerivativeThroughBaseContract(float offsetM)
        {
            TrackEdgeOffsetSegment segment = new TrackEdgeConstantOffsetSegment
            {
                startDistanceOnGeometryM = 25f,
                endDistanceOnGeometryM = 100f,
                offsetM = offsetM
            };

            foreach (float distance in new[] { 25f, 60f, 100f, 60f, 25f })
            {
                Assert.That(segment.EvaluateOffsetM(distance), Is.EqualTo(offsetM));
                Assert.That(segment.EvaluateDerivative(distance), Is.Zero);
            }

            Assert.That(segment.startDistanceOnGeometryM, Is.EqualTo(25f));
            Assert.That(segment.endDistanceOnGeometryM, Is.EqualTo(100f));
            Assert.That(((TrackEdgeConstantOffsetSegment)segment).offsetM, Is.EqualTo(offsetM));
        }
    }
}
