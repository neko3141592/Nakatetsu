using Nakatetsu.Track.Graph.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeCubicOffsetSegmentTests
    {
        private static TrackEdgeCubicOffsetSegment Cubic() => new()
        {
            startDistanceOnGeometryM = 20f,
            endDistanceOnGeometryM = 120f,
            startOffsetM = -2f,
            endOffsetM = 6f
        };

        [TestCase(false)]
        [TestCase(true)]
        public void CubicOffsetsAndEndpointSlopesWorkInEitherEdgeDirection(bool reverse)
        {
            var edge = new TrackEdgeDefinition
            {
                startDistanceOnGeometryM = reverse ? 120f : 20f,
                endDistanceOnGeometryM = reverse ? 20f : 120f
            };
            edge.offsetSegments.Add(Cubic());

            foreach (var (distance, expected, slope) in new[]
            {
                (20f, -2f, 0f), (45f, -0.75f, 0.09f), (70f, 2f, 0.12f),
                (95f, 4.75f, 0.09f), (120f, 6f, 0f)
            })
            {
                Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(distance, out float offset, out float derivative), Is.True);
                Assert.That(offset, Is.EqualTo(expected).Within(0.00001f));
                Assert.That(derivative, Is.EqualTo(slope).Within(0.000001f));
            }
        }

        [TestCase(45f)]
        [TestCase(70f)]
        [TestCase(95f)]
        public void DerivativeMatchesFiniteDifference(float distance)
        {
            var segment = Cubic();
            const float stepM = 0.05f;
            float difference = (segment.EvaluateOffsetM(distance + stepM)
                - segment.EvaluateOffsetM(distance - stepM)) / (2f * stepM);
            Assert.That(segment.EvaluateDerivative(distance), Is.EqualTo(difference).Within(0.0001f));
        }

        [TestCase(20f)]
        [TestCase(10f)]
        public void DegenerateLengthReturnsInvalidValues(float endDistance)
        {
            var segment = Cubic();
            segment.endDistanceOnGeometryM = endDistance;
            Assert.That(float.IsNaN(segment.EvaluateOffsetM(20f)), Is.True);
            Assert.That(float.IsNaN(segment.EvaluateDerivative(20f)), Is.True);
        }

        [TestCase(-8f)]
        [TestCase(0f)]
        public void NegativeAndZeroDisplacementsHaveMatchingSlopes(float displacementM)
        {
            var segment = Cubic();
            segment.endOffsetM = segment.startOffsetM + displacementM;
            Assert.That(segment.EvaluateOffsetM(70f), Is.EqualTo(-2f + displacementM / 2f));
            Assert.That(segment.EvaluateDerivative(70f), Is.EqualTo(displacementM * 0.015f).Within(0.000001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CubicJoinsConstantSegmentsAndCompilesIntoDistanceMap(bool reverse)
        {
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = 140f };
            geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { lengthM = 140f });
            var edge = new TrackEdgeDefinition
            {
                geometryId = "G", startDistanceOnGeometryM = reverse ? 140f : 0f,
                endDistanceOnGeometryM = reverse ? 0f : 140f
            };
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 20f, offsetM = -2f });
            edge.offsetSegments.Add(Cubic());
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 120f, endDistanceOnGeometryM = 140f, offsetM = 6f });

            foreach (float distance in new[] { 20f, 120f })
            {
                Assert.That(TrackEdgeCalculator.TryEvaluateAtGeometryDistance(edge, geometry, distance,
                    out var position, out var derivative), Is.True);
                Assert.That(position.x, Is.EqualTo(distance == 20f ? -2f : 6f));
                Assert.That(Vector3.Distance(derivative, Vector3.forward), Is.LessThan(0.000001f));
            }
            Assert.That(TrackEdgeCalculator.TryEvaluateAtGeometryDistance(edge, geometry, 70f,
                out var middle, out var middleDerivative), Is.True);
            Assert.That(Vector3.Distance(middle, new Vector3(2f, 0f, 70f)), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(middleDerivative, new Vector3(0.12f, 0f, 1f)), Is.LessThan(0.000001f));

            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(edge, geometry, 0.1f,
                out var map, out var error), Is.True, error);
            Assert.That(map.Exists(sample => sample.distanceOnGeometryM == 20f), Is.True);
            Assert.That(map.Exists(sample => sample.distanceOnGeometryM == 120f), Is.True);
            Assert.That(map[^1].distanceOnEdgeM, Is.GreaterThan(140f));
            edge.distanceMap = map;
            Assert.That(edge.TryConvertToGeometryDistance(edge.LengthM, out float end), Is.True);
            Assert.That(end, Is.EqualTo(edge.endDistanceOnGeometryM));
        }

        [Test]
        public void AssetRoundTripPreservesCubicOffsetTypeAndParameters()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CubicOffsetSerializationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                asset.Definition.offsetSegments.Add(Cubic());
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var loaded = AssetDatabase.LoadAssetAtPath<TrackEdgeAsset>(path);
                Assert.That(loaded.Definition.offsetSegments, Has.Count.EqualTo(1));
                Assert.That(loaded.Definition.offsetSegments[0], Is.TypeOf<TrackEdgeCubicOffsetSegment>());
                var segment = (TrackEdgeCubicOffsetSegment)loaded.Definition.offsetSegments[0];
                Assert.That(segment.startDistanceOnGeometryM, Is.EqualTo(20f));
                Assert.That(segment.endDistanceOnGeometryM, Is.EqualTo(120f));
                Assert.That(segment.startOffsetM, Is.EqualTo(-2f));
                Assert.That(segment.endOffsetM, Is.EqualTo(6f));
                Assert.That(segment.EvaluateOffsetM(45f), Is.EqualTo(-0.75f));
                Assert.That(segment.EvaluateDerivative(20f), Is.Zero);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }
    }
}
