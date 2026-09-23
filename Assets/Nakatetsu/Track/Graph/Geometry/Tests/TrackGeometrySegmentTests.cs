using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometrySegmentTests
    {
        private static IEnumerable<TrackGeometryHorizontalSegment> Shapes()
        {
            yield return new TrackGeometryStraightSegment();
            yield return new TrackGeometryCircularSegment { radiusM = -250f };
            yield return new TrackGeometryTransitionInSegment { radiusM = 400f };
            yield return new TrackGeometryTransitionOutSegment { radiusM = -400f };
        }

        [TestCase(500f, 0f)]
        [TestCase(500f, 50f)]
        [TestCase(500f, 100f)]
        [TestCase(-500f, 0f)]
        [TestCase(-500f, 50f)]
        [TestCase(-500f, 100f)]
        public void CircularDerivativeUsesLocalDistance(float radius, float localDistance)
        {
            var segment = new TrackGeometryCircularSegment
            {
                startDistanceM = 80f, lengthM = 100f, radiusM = radius
            };
            var derivative = segment.EvaluateDerivative(segment.startDistanceM + localDistance);
            double angle = (double)localDistance / radius;

            Assert.That(derivative.x, Is.EqualTo(Math.Sin(angle)).Within(0.000001));
            Assert.That(derivative.y, Is.Zero);
            Assert.That(derivative.z, Is.EqualTo(Math.Cos(angle)).Within(0.000001));
        }

        [TestCase(500f)]
        [TestCase(-500f)]
        public void CircularDerivativeMatchesPositionDifference(float radius)
        {
            var segment = new TrackGeometryCircularSegment
            {
                startDistanceM = 80f, lengthM = 100f, radiusM = radius
            };
            const float distance = 130f;
            const float step = 0.5f;
            segment.EvaluatePosition(distance + step, out var after, out _);
            segment.EvaluatePosition(distance - step, out var before, out _);
            var numericalDerivative = (after - before) / (2f * step);

            Assert.That(Vector3.Distance(segment.EvaluateDerivative(distance), numericalDerivative), Is.LessThan(0.0001f));
        }

        [TestCase(0f)]
        [TestCase(0.0005f)]
        [TestCase(-0.0005f)]
        public void CircularDerivativeRetainsStraightFallback(float radius)
        {
            var segment = new TrackGeometryCircularSegment { startDistanceM = 80f, radiusM = radius };

            Assert.That(segment.EvaluateDerivative(105f), Is.EqualTo(new Vector3(0f, 0f, 1f)));
        }

        [TestCase(500f, 0f, 0f)]
        [TestCase(500f, 50f, 0.025f)]
        [TestCase(500f, 100f, 0.1f)]
        [TestCase(-500f, 0f, 0f)]
        [TestCase(-500f, 50f, -0.025f)]
        [TestCase(-500f, 100f, -0.1f)]
        public void TransitionInDerivativeUsesLocalDistanceWithoutNormalization(float radius, float localDistance, float expectedX)
        {
            var segment = new TrackGeometryTransitionInSegment
            {
                startDistanceM = 80f, lengthM = 100f, radiusM = radius
            };
            var derivative = segment.EvaluateDerivative(segment.startDistanceM + localDistance);

            Assert.That(derivative.x, Is.EqualTo(expectedX).Within(0.000001f));
            Assert.That(derivative.y, Is.Zero);
            Assert.That(derivative.z, Is.EqualTo(1f));
        }

        [TestCase(0f, 100f)]
        [TestCase(0.0005f, 100f)]
        [TestCase(-0.0005f, 100f)]
        [TestCase(500f, 0f)]
        [TestCase(500f, 0.0005f)]
        [TestCase(500f, -100f)]
        public void TransitionInDerivativeRetainsStraightFallback(float radius, float length)
        {
            var segment = new TrackGeometryTransitionInSegment
            {
                startDistanceM = 80f, lengthM = length, radiusM = radius
            };

            Assert.That(segment.EvaluateDerivative(105f), Is.EqualTo(new Vector3(0f, 0f, 1f)));
        }

        [TestCase(500f, 0f, 0f)]
        [TestCase(500f, 50f, 0.075f)]
        [TestCase(500f, 100f, 0.1f)]
        [TestCase(-500f, 0f, 0f)]
        [TestCase(-500f, 50f, -0.075f)]
        [TestCase(-500f, 100f, -0.1f)]
        public void TransitionOutDerivativeUsesLocalDistanceWithoutNormalization(float radius, float localDistance, float expectedX)
        {
            var segment = new TrackGeometryTransitionOutSegment
            {
                startDistanceM = 80f, lengthM = 100f, radiusM = radius
            };

            var derivative = segment.EvaluateDerivative(segment.startDistanceM + localDistance);

            Assert.That(derivative.x, Is.EqualTo(expectedX).Within(0.000001f));
            Assert.That(derivative.y, Is.Zero);
            Assert.That(derivative.z, Is.EqualTo(1f));
        }

        [TestCase(500f)]
        [TestCase(-500f)]
        public void TransitionOutDerivativeMatchesPositionDifference(float radius)
        {
            var segment = new TrackGeometryTransitionOutSegment
            {
                startDistanceM = 80f, lengthM = 100f, radiusM = radius
            };
            const float distance = 130f;
            const float step = 0.125f;
            segment.EvaluatePosition(distance + step, out var after, out _);
            segment.EvaluatePosition(distance - step, out var before, out _);
            var numericalDerivative = (after - before) / (2f * step);

            Assert.That(Vector3.Distance(segment.EvaluateDerivative(distance), numericalDerivative), Is.LessThan(0.00001f));
        }

        [TestCase(0f, 100f)]
        [TestCase(0.0005f, 100f)]
        [TestCase(-0.0005f, 100f)]
        [TestCase(500f, 0f)]
        [TestCase(500f, 0.0005f)]
        [TestCase(500f, -100f)]
        public void TransitionOutDerivativeRetainsStraightFallback(float radius, float length)
        {
            var segment = new TrackGeometryTransitionOutSegment
            {
                startDistanceM = 80f, lengthM = length, radiusM = radius
            };

            Assert.That(segment.EvaluateDerivative(105f), Is.EqualTo(new Vector3(0f, 0f, 1f)));
        }

        [TestCaseSource(nameof(Shapes))]
        public void PositionUsesAbsoluteGeometryDistanceAndRetainsLegacyFormula(TrackGeometryHorizontalSegment segment)
        {
            segment.startDistanceM = 80f;
            segment.lengthM = 100f;
            segment.EvaluatePosition(105f, out var position, out float heading);
            float x, z, expectedHeading;
            switch (segment)
            {
                case TrackGeometryCircularSegment circular:
                    x = (float)(circular.radiusM * (1.0 - Math.Cos(25.0 / circular.radiusM)));
                    z = (float)(circular.radiusM * Math.Sin(25.0 / circular.radiusM));
                    expectedHeading = (float)(25.0 / circular.radiusM * 180.0 / Math.PI);
                    break;
                case TrackGeometryTransitionInSegment transitionIn:
                    x = (float)(25.0 * 25.0 * 25.0 / (6.0 * 100.0 * transitionIn.radiusM));
                    z = 25f;
                    expectedHeading = (float)(25.0 * 25.0 / (2.0 * 100.0 * transitionIn.radiusM) * 180.0 / Math.PI);
                    break;
                case TrackGeometryTransitionOutSegment transitionOut:
                    x = (float)(25.0 * 25.0 / (2.0 * transitionOut.radiusM)
                        - 25.0 * 25.0 * 25.0 / (6.0 * transitionOut.radiusM * 100.0));
                    z = 25f;
                    expectedHeading = (float)((25.0 / transitionOut.radiusM
                        - 25.0 * 25.0 / (2.0 * transitionOut.radiusM * 100.0)) * 180.0 / Math.PI);
                    break;
                default:
                    x = 0f;
                    z = 25f;
                    expectedHeading = 0f;
                    break;
            }
            Assert.That(Vector3.Distance(position, new Vector3(x, 0f, z)), Is.LessThan(0.0001f));
            Assert.That(heading, Is.EqualTo(expectedHeading).Within(0.0001f));
        }

        private static IEnumerable<TrackGeometryHorizontalSegment> DegenerateShapes()
        {
            yield return new TrackGeometryCircularSegment { radiusM = 0.0005f };
            yield return new TrackGeometryCircularSegment { radiusM = -0.0005f };
            yield return new TrackGeometryTransitionInSegment { radiusM = 0.0005f };
            yield return new TrackGeometryTransitionOutSegment { radiusM = -0.0005f };
            yield return new TrackGeometryTransitionInSegment { lengthM = 0.0005f };
            yield return new TrackGeometryTransitionOutSegment { lengthM = 0.0005f };
        }

        [TestCaseSource(nameof(DegenerateShapes))]
        public void DegenerateShapesRetainStraightFallback(TrackGeometryHorizontalSegment segment)
        {
            segment.startDistanceM = 80f;
            float distance = segment.lengthM / 2f;
            float geometryDistance = segment.startDistanceM + distance;
            segment.EvaluatePosition(geometryDistance, out var position, out float heading);
            Assert.That(position, Is.EqualTo(new Vector3(0f, 0f, geometryDistance - segment.startDistanceM)));
            Assert.That(heading, Is.Zero);
        }

        [TestCase(0, typeof(TrackGeometryStraightSegment))]
        [TestCase(1, typeof(TrackGeometryCircularSegment))]
        [TestCase(2, typeof(TrackGeometryTransitionInSegment))]
        [TestCase(3, typeof(TrackGeometryTransitionOutSegment))]
        public void LegacyInlineSegmentLoadsAndResavesAsDerivedType(int oldType, Type expectedType)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/GeometrySegmentMigrationTest.asset");
            try
            {
                // Keep the old format independent of the sample asset's future authoring format.
                string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_Script: {fileID: 11500000, guid: 867e6fab413b4b9cb8875a007ce199af, type: 3}\n" +
                    "  m_Name: GeometrySegmentMigrationTest\n  definition:\n" +
                    "    trackGeometryId: Legacy\n    lengthM: 120\n    originRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
                    "    horizontalSegments:\n    - startDistanceM: 20\n      lengthM: 100\n" +
                    $"      trackCurveType: {oldType}\n      radiusM: -250\n    verticalSegments: []\n";
                File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path);
                Assert.That(asset, Is.Not.Null);
                var segment = asset.Definition.horizontalSegments[0];
                Assert.That(segment.GetType(), Is.EqualTo(expectedType));
                Assert.That(segment.startDistanceM, Is.EqualTo(20f));
                Assert.That(segment.lengthM, Is.EqualTo(100f));
                AssertRadius(segment, -250f);
                segment.EvaluatePosition(70f, out var beforePosition, out float beforeHeading);

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path);
                var restored = reloaded.Definition.horizontalSegments[0];
                Assert.That(restored.GetType(), Is.EqualTo(expectedType));
                AssertRadius(restored, -250f);
                restored.EvaluatePosition(70f, out var afterPosition, out float afterHeading);
                Assert.That(afterPosition, Is.EqualTo(beforePosition));
                Assert.That(afterHeading, Is.EqualTo(beforeHeading));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void MixedShapeListPreservesTypesOrderAndParameters()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/GeometrySegmentRoundTripTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackGeometryAsset>();
            try
            {
                asset.name = "GeometrySegmentRoundTripTest";
                asset.Definition.lengthM = 400f;
                var shapes = new List<TrackGeometryHorizontalSegment>(Shapes());
                for (int i = 0; i < shapes.Count; i++) shapes[i].startDistanceM = i * 100f;
                asset.Definition.horizontalSegments = shapes;
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path);
                var restored = loaded.Definition.horizontalSegments;
                Assert.That(restored, Has.Count.EqualTo(4));
                for (int i = 0; i < shapes.Count; i++)
                {
                    Assert.That(restored[i].GetType(), Is.EqualTo(shapes[i].GetType()));
                    Assert.That(restored[i].startDistanceM, Is.EqualTo(i * 100f));
                    Assert.That(restored[i].lengthM, Is.EqualTo(100f));
                    shapes[i].EvaluatePosition(i * 100f + 50f, out var expectedPosition, out float expectedHeading);
                    restored[i].EvaluatePosition(i * 100f + 50f, out var position, out float heading);
                    Assert.That(position, Is.EqualTo(expectedPosition));
                    Assert.That(heading, Is.EqualTo(expectedHeading));
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }

        private static void AssertRadius(TrackGeometryHorizontalSegment segment, float expected)
        {
            switch (segment)
            {
                case TrackGeometryCircularSegment circular: Assert.That(circular.radiusM, Is.EqualTo(expected)); break;
                case TrackGeometryTransitionInSegment transitionIn: Assert.That(transitionIn.radiusM, Is.EqualTo(expected)); break;
                case TrackGeometryTransitionOutSegment transitionOut: Assert.That(transitionOut.radiusM, Is.EqualTo(expected)); break;
            }
        }
    }
}
