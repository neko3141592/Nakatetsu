using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeTransitionOffsetSegmentTests
    {
        private static TrackEdgeTransitionOffsetSegment Segment(bool entry, float radius = 600f)
        {
            TrackEdgeTransitionOffsetSegment segment = entry
                ? new TrackEdgeTransitionInOffsetSegment()
                : new TrackEdgeTransitionOutOffsetSegment();
            segment.startDistanceOnGeometryM = 1000f;
            segment.endDistanceOnGeometryM = 1020f;
            segment.startOffsetM = 4f;
            segment.startHeadingDeg = 2f;
            segment.radiusM = radius;
            return segment;
        }

        [TestCase(true, 600f)]
        [TestCase(false, 600f)]
        [TestCase(true, -600f)]
        [TestCase(false, -600f)]
        public void PositionDerivativeAndCurvatureAgree(bool entry, float radius)
        {
            var segment = Segment(entry, radius);
            Assert.That(segment.EvaluateOffsetM(1000f), Is.EqualTo(4f));
            Assert.That(segment.EvaluateDerivative(1000f),
                Is.EqualTo(Math.Tan(2 * Math.PI / 180)).Within(1e-7));

            foreach (float distance in new[] { 1004f, 1010f, 1016f })
            {
                const float step = 0.25f;
                double slope = segment.EvaluateDerivative(distance);
                double difference = (segment.EvaluateOffsetM(distance + step)
                    - segment.EvaluateOffsetM(distance - step)) / (2.0 * step);
                Assert.That(difference, Is.EqualTo(slope).Within(2e-6));
                double second = (segment.EvaluateDerivative(distance + step)
                    - segment.EvaluateDerivative(distance - step)) / (2.0 * step);
                double curvature = second / Math.Pow(1 + slope * slope, 1.5);
                double expected = (entry ? (distance - 1000) / 20 : (1020 - distance) / 20) / radius;
                Assert.That(curvature, Is.EqualTo(expected).Within(1e-7));
                Assert.That(segment.EvaluateCurvaturePerM(distance), Is.EqualTo(expected).Within(1e-9));
            }

            // 高分解能の台形積分を独立した位置の参照値として使う。
            const int samples = 10000;
            double sum = 0;
            double previous = segment.EvaluateDerivative(1000f);
            double previousDistance = 1000;
            for (int i = 1; i <= samples; i++)
            {
                float distance = 1000f + 20f * i / samples;
                double current = segment.EvaluateDerivative(distance);
                sum += (previous + current) / 2 * (distance - previousDistance);
                previous = current;
                previousDistance = distance;
            }
            Assert.That(segment.EvaluateOffsetM(1020f), Is.EqualTo(4 + sum).Within(1e-6));
            Assert.That(segment.EvaluateCurvaturePerM(1000f), Is.EqualTo(entry ? 0 : 1f / radius));
            Assert.That(segment.EvaluateCurvaturePerM(1020f), Is.EqualTo(entry ? 1f / radius : 0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SignedRadiusAndHeadingMirrorTheCurve(bool entry)
        {
            var right = Segment(entry);
            var left = Segment(entry, -600f);
            left.startOffsetM = -right.startOffsetM;
            left.startHeadingDeg = -right.startHeadingDeg;
            foreach (float distance in new[] { 1000f, 1005f, 1010f, 1020f })
            {
                Assert.That(left.EvaluateOffsetM(distance), Is.EqualTo(-right.EvaluateOffsetM(distance)).Within(1e-6));
                Assert.That(left.EvaluateDerivative(distance), Is.EqualTo(-right.EvaluateDerivative(distance)).Within(1e-7));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InvalidDefinitionsAndOutOfRangeInputsFail(bool entry)
        {
            foreach (Action<TrackEdgeTransitionOffsetSegment> invalidate in new Action<TrackEdgeTransitionOffsetSegment>[]
            {
                s => s.radiusM = 0, s => s.radiusM = float.NaN,
                s => s.radiusM = float.PositiveInfinity, s => s.radiusM = 5,
                s => s.startHeadingDeg = 90, s => s.startHeadingDeg = float.NaN,
                s => s.startOffsetM = float.PositiveInfinity,
                s => s.startDistanceOnGeometryM = -1, s => s.endDistanceOnGeometryM = 1000,
                s => s.endDistanceOnGeometryM = float.PositiveInfinity
            })
            {
                var segment = Segment(entry);
                invalidate(segment);
                Assert.That(float.IsNaN(segment.EvaluateOffsetM(1000f)), Is.True);
                Assert.That(float.IsNaN(segment.EvaluateDerivative(1000f)), Is.True);
            }
            foreach (float distance in new[] { 999f, 1021f, float.NaN, float.PositiveInfinity })
            {
                var segment = Segment(entry);
                Assert.That(float.IsNaN(segment.EvaluateOffsetM(distance)), Is.True);
                Assert.That(float.IsNaN(segment.EvaluateDerivative(distance)), Is.True);
                Assert.That(float.IsNaN(segment.EvaluateCurvaturePerM(distance)), Is.True);
            }
        }

        private static List<TrackEdgeOffsetSegment> JoinedCurve()
        {
            var entry = new TrackEdgeTransitionInOffsetSegment
            {
                startDistanceOnGeometryM = 1000, endDistanceOnGeometryM = 1020,
                startOffsetM = 0, radiusM = 600, startHeadingDeg = 0
            };
            var arc = new TrackEdgeArcOffsetSegment
            {
                startDistanceOnGeometryM = 1020, endDistanceOnGeometryM = 1050,
                startOffsetM = entry.EvaluateOffsetM(1020), radiusM = 600,
                startHeadingDeg = (float)(Math.Atan(entry.EvaluateDerivative(1020)) * 180 / Math.PI)
            };
            var exit = new TrackEdgeTransitionOutOffsetSegment
            {
                startDistanceOnGeometryM = 1050, endDistanceOnGeometryM = 1070,
                startOffsetM = arc.EvaluateOffsetM(1050), radiusM = 600,
                startHeadingDeg = (float)(Math.Atan(arc.EvaluateDerivative(1050)) * 180 / Math.PI)
            };
            return new List<TrackEdgeOffsetSegment>
            {
                new TrackEdgeConstantOffsetSegment
                { startDistanceOnGeometryM = 990, endDistanceOnGeometryM = 1000, offsetM = 0 },
                entry, arc, exit,
                new TrackEdgeLinearOffsetSegment
                {
                    startDistanceOnGeometryM = 1070, endDistanceOnGeometryM = 1080,
                    startOffsetM = exit.EvaluateOffsetM(1070),
                    endOffsetM = exit.EvaluateOffsetM(1070) + 10 * exit.EvaluateDerivative(1070)
                }
            };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JoinedTransitionsHaveContinuousTangentsAndWorkInEitherEdgeDirection(bool reverse)
        {
            var segments = JoinedCurve();
            for (int i = 1; i < segments.Count; i++)
            {
                float distance = segments[i].startDistanceOnGeometryM;
                Assert.That(segments[i].EvaluateOffsetM(distance),
                    Is.EqualTo(segments[i - 1].EvaluateOffsetM(distance)).Within(1e-5));
                Assert.That(segments[i].EvaluateDerivative(distance),
                    Is.EqualTo(segments[i - 1].EvaluateDerivative(distance)).Within(1e-6));
            }
            var edge = new TrackEdgeDefinition
            {
                geometryId = "G",
                startDistanceOnGeometryM = reverse ? 1080 : 990,
                endDistanceOnGeometryM = reverse ? 990 : 1080, offsetSegments = segments
            };
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = 1100 };
            geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { lengthM = 1100 });
            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(edge, geometry, 0.25f, out var map, out var error),
                Is.True, error);
            foreach (float boundary in new[] { 1000f, 1020f, 1050f, 1070f })
                Assert.That(map.Exists(sample => sample.distanceOnGeometryM == boundary), Is.True);
            edge.distanceMap = map;
            Assert.That(edge.TryConvertToGeometryDistance(edge.LengthM, out float end), Is.True);
            Assert.That(end, Is.EqualTo(edge.endDistanceOnGeometryM));
        }

        [Test]
        public void AssetSerializationRetainsBothTransitionTypesAndInheritedParameters()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TransitionOffsetSerializationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                asset.Definition.offsetSegments = JoinedCurve();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadAssetAtPath<TrackEdgeAsset>(path);
                Assert.That(loaded.Definition.offsetSegments[1], Is.TypeOf<TrackEdgeTransitionInOffsetSegment>());
                Assert.That(loaded.Definition.offsetSegments[3], Is.TypeOf<TrackEdgeTransitionOutOffsetSegment>());
                var entry = (TrackEdgeTransitionInOffsetSegment)loaded.Definition.offsetSegments[1];
                Assert.That(entry.radiusM, Is.EqualTo(600));
                Assert.That(entry.startDistanceOnGeometryM, Is.EqualTo(1000));
                Assert.That(entry.EvaluateDerivative(1000), Is.Zero);
                Assert.That(entry.EvaluateCurvaturePerM(1020), Is.EqualTo(1f / 600));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
