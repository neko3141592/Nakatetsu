using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeCubicHermiteOffsetSegmentTests
    {
        private static TrackEdgeCubicHermiteOffsetSegment Segment() => new()
        {
            startDistanceOnGeometryM = 20f,
            endDistanceOnGeometryM = 120f,
            startOffsetM = -2f,
            endOffsetM = 6f,
            startSlope = 0.06f,
            endSlope = -0.04f
        };

        [TestCase(false)]
        [TestCase(true)]
        public void NonzeroEndpointTangentsArePreservedInEitherEdgeDirection(bool reverse)
        {
            var segment = Segment();
            var edge = new TrackEdgeDefinition
            {
                startDistanceOnGeometryM = reverse ? 120f : 20f,
                endDistanceOnGeometryM = reverse ? 20f : 120f
            };
            edge.offsetSegments.Add(segment);

            foreach (var (distance, offset, slope) in new[]
            {
                (20f, -2f, 0.06f), (120f, 6f, -0.04f)
            })
            {
                Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(distance,
                    out float actualOffset, out float actualSlope), Is.True);
                Assert.That(actualOffset, Is.EqualTo(offset).Within(1e-6));
                Assert.That(actualSlope, Is.EqualTo(slope).Within(1e-7));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void OldNT07WestCubicParabolaPositionsAndTangentsArePreserved(int curve)
        {
            // Independent samples from old TD-ATC E_NT07_P01_040. These are the
            // two cubic parabolas, shifted by +2 m into the new offset system.
            // The first reverses curvature; the second ends at a straight.
            var lengths = new[] { 54.585505f, 20f };
            var offsets = new[]
            {
                new[] { -2.6875, -4.446310152711669, -6.476205136987752,
                    -8.506100121263835, -10.264910273975502 },
                new[] { -12.30929, -12.419544224096162, -12.476161247348426,
                    -12.49702013670476, -12.499999959113127 }
            };
            var slopes = new[]
            {
                new[] { -0.11233063623084469, -0.14212808867516105, -0.15206057282326652,
                    -0.14212808867516102, -0.11233063623084466 },
                new[] { -0.02860650270015314, -0.016091155836577116, -0.007151622362594239,
                    -0.0017879022782045124, 0.0000000044165920608241205 }
            };
            var segment = new TrackEdgeCubicHermiteOffsetSegment
            {
                startDistanceOnGeometryM = 0f,
                endDistanceOnGeometryM = lengths[curve],
                startOffsetM = (float)offsets[curve][0],
                endOffsetM = (float)offsets[curve][4],
                startSlope = (float)slopes[curve][0],
                endSlope = (float)slopes[curve][4]
            };

            for (int i = 0; i <= 4; i++)
            {
                float distance = lengths[curve] * i / 4f;
                Assert.That(segment.EvaluateOffsetM(distance),
                    Is.EqualTo(offsets[curve][i]).Within(2e-6), $"curve {curve}, sample {i}");
                Assert.That(segment.EvaluateDerivative(distance),
                    Is.EqualTo(slopes[curve][i]).Within(2e-7), $"curve {curve}, sample {i}");
            }
        }

        [TestCase(-8f)]
        [TestCase(0f)]
        [TestCase(8f)]
        public void ZeroSlopesRetainExistingCubicShape(float displacement)
        {
            var hermite = Segment();
            hermite.endOffsetM = hermite.startOffsetM + displacement;
            hermite.startSlope = hermite.endSlope = 0f;
            var cubic = new TrackEdgeCubicOffsetSegment
            {
                startDistanceOnGeometryM = hermite.startDistanceOnGeometryM,
                endDistanceOnGeometryM = hermite.endDistanceOnGeometryM,
                startOffsetM = hermite.startOffsetM,
                endOffsetM = hermite.endOffsetM
            };

            foreach (float distance in new[] { 20f, 45f, 70f, 95f, 120f })
            {
                Assert.That(hermite.EvaluateOffsetM(distance),
                    Is.EqualTo(cubic.EvaluateOffsetM(distance)).Within(1e-6));
                Assert.That(hermite.EvaluateDerivative(distance),
                    Is.EqualTo(cubic.EvaluateDerivative(distance)).Within(1e-7));
            }
        }

        [TestCase(45f)]
        [TestCase(70f)]
        [TestCase(95f)]
        public void NonzeroSlopeDerivativeMatchesFiniteDifference(float distance)
        {
            var segment = Segment();
            const float step = 0.05f;
            float difference = (segment.EvaluateOffsetM(distance + step)
                - segment.EvaluateOffsetM(distance - step)) / (2f * step);
            Assert.That(segment.EvaluateDerivative(distance), Is.EqualTo(difference).Within(1e-4));
        }

        [Test]
        public void DegenerateLengthsAndNonfiniteParametersReturnInvalidValues()
        {
            foreach (Action<TrackEdgeCubicHermiteOffsetSegment> invalidate in
                new Action<TrackEdgeCubicHermiteOffsetSegment>[]
                {
                    s => s.endDistanceOnGeometryM = s.startDistanceOnGeometryM,
                    s => s.endDistanceOnGeometryM = s.startDistanceOnGeometryM - 1f,
                    s => s.startDistanceOnGeometryM = float.NaN,
                    s => s.startDistanceOnGeometryM = float.NegativeInfinity,
                    s => s.endDistanceOnGeometryM = float.NaN,
                    s => s.endDistanceOnGeometryM = float.PositiveInfinity,
                    s => s.startOffsetM = float.NaN,
                    s => s.startOffsetM = float.PositiveInfinity,
                    s => s.endOffsetM = float.NaN,
                    s => s.endOffsetM = float.NegativeInfinity,
                    s => s.startSlope = float.NaN,
                    s => s.startSlope = float.PositiveInfinity,
                    s => s.endSlope = float.NaN,
                    s => s.endSlope = float.NegativeInfinity
                })
            {
                var segment = Segment();
                invalidate(segment);
                Assert.That(float.IsNaN(segment.EvaluateOffsetM(20f)), Is.True);
                Assert.That(float.IsNaN(segment.EvaluateDerivative(20f)), Is.True);
            }
        }

        [TestCase(19f)]
        [TestCase(121f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void OutOfRangeAndNonfiniteInputsReturnInvalidValues(float distance)
        {
            var segment = Segment();
            Assert.That(float.IsNaN(segment.EvaluateOffsetM(distance)), Is.True);
            Assert.That(float.IsNaN(segment.EvaluateDerivative(distance)), Is.True);
        }

        [Test]
        public void NativeAssetRoundTripPreservesHermiteTypeParametersAndTangents()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CubicHermiteSerializationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                asset.Definition.offsetSegments.Add(Segment());
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var loaded = AssetDatabase.LoadAssetAtPath<TrackEdgeAsset>(path);
                Assert.That(loaded.Definition.offsetSegments, Has.Count.EqualTo(1));
                Assert.That(loaded.Definition.offsetSegments[0], Is.TypeOf<TrackEdgeCubicHermiteOffsetSegment>());
                var segment = (TrackEdgeCubicHermiteOffsetSegment)loaded.Definition.offsetSegments[0];
                Assert.That(segment.startDistanceOnGeometryM, Is.EqualTo(20f));
                Assert.That(segment.endDistanceOnGeometryM, Is.EqualTo(120f));
                Assert.That(segment.startOffsetM, Is.EqualTo(-2f));
                Assert.That(segment.endOffsetM, Is.EqualTo(6f));
                Assert.That(segment.startSlope, Is.EqualTo(0.06f));
                Assert.That(segment.endSlope, Is.EqualTo(-0.04f));
                Assert.That(segment.EvaluateDerivative(20f), Is.EqualTo(0.06f).Within(1e-7));
                Assert.That(segment.EvaluateDerivative(120f), Is.EqualTo(-0.04f).Within(1e-7));
                Assert.That(segment.EvaluateOffsetM(70f), Is.EqualTo(3.25f).Within(1e-6));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
