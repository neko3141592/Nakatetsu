using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometryVerticalSegmentTests
    {
        [TestCase(20f)]
        [TestCase(-20f)]
        [TestCase(0f)]
        public void ConstantGradientUsesAbsoluteDistanceAndReturnsDerivativeInMetresPerMetre(float gradient)
        {
            var segment = new TrackGeometryConstantGradientSegment
            { startDistanceM = 80f, lengthM = 100f, gradientPermille = gradient };
            foreach (float local in new[] { 0f, 50f, 100f })
            {
                Assert.That(segment.EvaluateHeightDeltaM(80f + local), Is.EqualTo(gradient * local / 1000f).Within(0.000001f));
                Assert.That(segment.EvaluateDerivative(80f + local), Is.EqualTo(gradient / 1000f).Within(0.000001f));
            }
        }

        [TestCase(0f, 20f)]
        [TestCase(20f, 0f)]
        [TestCase(-20f, 20f)]
        [TestCase(20f, 20f)]
        public void LinearGradientMatchesIntegratedHeightAndAnalyticDerivative(float startGradient, float endGradient)
        {
            var segment = new TrackGeometryLinearGradientSegment
            {
                startDistanceM = 80f, lengthM = 100f,
                startGradientPermille = startGradient, endGradientPermille = endGradient
            };
            foreach (float local in new[] { 0f, 50f, 100f })
            {
                double expectedHeight = (startGradient * (double)local
                    + (endGradient - startGradient) * (double)local * local / 200.0) / 1000.0;
                double expectedDerivative = (startGradient + (endGradient - startGradient) * local / 100.0) / 1000.0;
                Assert.That(segment.EvaluateHeightDeltaM(80f + local), Is.EqualTo(expectedHeight).Within(0.000001));
                Assert.That(segment.EvaluateDerivative(80f + local), Is.EqualTo(expectedDerivative).Within(0.000001));
            }
        }

        private static IEnumerable<TrackGeometryVerticalSegment> Shapes()
        {
            yield return new TrackGeometryConstantGradientSegment { gradientPermille = -20f };
            yield return new TrackGeometryLinearGradientSegment { startGradientPermille = -10f, endGradientPermille = 30f };
        }

        [TestCaseSource(nameof(Shapes))]
        public void DerivativeMatchesHeightDifference(TrackGeometryVerticalSegment segment)
        {
            segment.startDistanceM = 80f;
            const float distance = 130f;
            const float step = 0.125f;
            float numerical = (segment.EvaluateHeightDeltaM(distance + step) - segment.EvaluateHeightDeltaM(distance - step)) / (2f * step);
            Assert.That(segment.EvaluateDerivative(distance), Is.EqualTo(numerical).Within(0.000001f));
        }

        [TestCaseSource(nameof(Shapes))]
        public void DegenerateLengthsAreFiniteAndSkipped(TrackGeometryVerticalSegment segment)
        {
            segment.startDistanceM = 80f;
            foreach (float length in new[] { -1f, 0f, 0.0005f, 0.001f })
            {
                segment.lengthM = length;
                Assert.That(segment.EvaluateHeightDeltaM(80f), Is.Zero);
                Assert.That(segment.EvaluateDerivative(80f), Is.Zero);
                var definition = new TrackGeometryDefinition();
                definition.verticalSegments.Add(segment);
                Assert.That(TrackGeometryProfileCalculator.GetVerticalHeightAt(definition.verticalSegments, 100f), Is.Zero);
                Assert.That(TrackGeometryProfileCalculator.GetGradientPermilleAt(definition.verticalSegments, 100f), Is.Zero);
                var context = new TrackGeometryContext();
                TrackGeometryCompiler.Rebuild(definition, context);
                Assert.That(context.Workspace.verticalStarts, Is.Empty);
            }
        }

        [TestCase(-10f, 0f, 0f)]
        [TestCase(10f, 0f, 0f)]
        [TestCase(20f, 0f, 20f)]
        [TestCase(70f, 1f, 20f)]
        [TestCase(90f, 1.4f, 20f)]
        [TestCase(100f, 1.6f, 20f)]
        [TestCase(125f, 1.975f, 10f)]
        [TestCase(150f, 2.1f, 0f)]
        [TestCase(175f, 2.1f, 0f)]
        public void MixedProfilePreservesHeightGradientAndGapRules(float distance, float height, float gradient)
        {
            var definition = MixedDefinition();
            Assert.That(TrackGeometryProfileCalculator.GetVerticalHeightAt(definition.verticalSegments, distance), Is.EqualTo(height).Within(0.000001f));
            Assert.That(TrackGeometryProfileCalculator.GetGradientPermilleAt(definition.verticalSegments, distance), Is.EqualTo(gradient).Within(0.00001f));
            var context = new TrackGeometryContext();
            TrackGeometryCompiler.Rebuild(definition, context);
            Assert.That(context.Workspace.verticalStarts.Count, Is.EqualTo(2));
            Assert.That(context.Workspace.verticalStarts[1].heightM, Is.EqualTo(1.6f).Within(0.000001f));
        }

        [TestCase(0f, 20f)]
        [TestCase(-20f, -20f)]
        public void LegacyVerticalOnlyAssetLoadsAndResaves(float startGradient, float endGradient)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/VerticalMigrationTest.asset");
            try
            {
                string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_Script: {fileID: 11500000, guid: 867e6fab413b4b9cb8875a007ce199af, type: 3}\n" +
                    "  m_Name: VerticalMigrationTest\n  definition:\n" +
                    "    trackGeometryId: LegacyVertical\n    lengthM: 120\n    horizontalSegments: []\n" +
                    "    verticalSegments:\n    - startDistanceM: 20\n      lengthM: 100\n" +
                    $"      startGradientPermille: {startGradient}\n      endGradientPermille: {endGradient}\n";
                File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path);
                AssertMigrated(asset, startGradient, endGradient);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssertMigrated(AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path), startGradient, endGradient);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void MixedVerticalTypesSurviveAssetRoundTrip()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/VerticalRoundTripTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackGeometryAsset>();
            try
            {
                asset.Definition.verticalSegments = MixedDefinition().verticalSegments;
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var restored = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path).Definition.verticalSegments;
                Assert.That(restored.Count, Is.EqualTo(2));
                Assert.That(restored[0], Is.TypeOf<TrackGeometryConstantGradientSegment>());
                Assert.That(restored[1], Is.TypeOf<TrackGeometryLinearGradientSegment>());
                Assert.That(restored[0].startDistanceM, Is.EqualTo(20f));
                Assert.That(restored[1].startDistanceM, Is.EqualTo(100f));
                Assert.That(restored[0].lengthM, Is.EqualTo(50f));
                Assert.That(restored[1].lengthM, Is.EqualTo(50f));
                Assert.That(((TrackGeometryConstantGradientSegment)restored[0]).gradientPermille, Is.EqualTo(20f));
                Assert.That(((TrackGeometryLinearGradientSegment)restored[1]).startGradientPermille, Is.EqualTo(20f));
                Assert.That(((TrackGeometryLinearGradientSegment)restored[1]).endGradientPermille, Is.Zero);
                Assert.That(TrackGeometryProfileCalculator.GetVerticalHeightAt(restored, 125f), Is.EqualTo(1.975f).Within(0.000001f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }

        private static TrackGeometryDefinition MixedDefinition()
        {
            return new TrackGeometryDefinition
            {
                verticalSegments = new List<TrackGeometryVerticalSegment>
                {
                    new TrackGeometryConstantGradientSegment { startDistanceM = 20f, lengthM = 50f, gradientPermille = 20f },
                    new TrackGeometryLinearGradientSegment { startDistanceM = 100f, lengthM = 50f, startGradientPermille = 20f, endGradientPermille = 0f }
                }
            };
        }

        private static void AssertMigrated(TrackGeometryAsset asset, float startGradient, float endGradient)
        {
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Definition.verticalSegments.Count, Is.EqualTo(1));
            Assert.That(asset.Definition.verticalSegments[0], Is.TypeOf<TrackGeometryLinearGradientSegment>());
            var segment = (TrackGeometryLinearGradientSegment)asset.Definition.verticalSegments[0];
            Assert.That(segment.startDistanceM, Is.EqualTo(20f));
            Assert.That(segment.lengthM, Is.EqualTo(100f));
            Assert.That(segment.startGradientPermille, Is.EqualTo(startGradient));
            Assert.That(segment.endGradientPermille, Is.EqualTo(endGradient));
            Assert.That(segment.EvaluateHeightDeltaM(120f), Is.EqualTo((startGradient + endGradient) * 0.05f).Within(0.000001f));
            Assert.That(segment.EvaluateDerivative(120f), Is.EqualTo(endGradient / 1000f).Within(0.000001f));
        }
    }
}
