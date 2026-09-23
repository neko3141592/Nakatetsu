using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Nakatetsu.Track.Graph.Geometry;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometryAssetTests
    {
        [Test]
        public void IncludedStraightAssetLoadsAndEvaluates()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(
                "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrackGeometryStraight100m.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Definition.trackGeometryId, Is.EqualTo("SandboxStraight100m"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)),
                Is.EqualTo("8d0abc7bf4be4bdfbc7d7d66a15fe788"));
            Assert.That(asset.TryEvaluate(100f, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(0, 0, 100)), Is.LessThan(0.0001f));
        }

        [TestCase("guideLineId")]
        [TestCase("trainGeometryId")]
        public void LegacyDefinitionIdIsPreserved(string legacyFieldName)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/LegacyTrackGeometry.asset");
            try
            {
                string yaml = File.ReadAllText(
                    "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrackGeometryStraight100m.asset");
                File.WriteAllText(path, yaml.Replace("trackGeometryId:", legacyFieldName + ":"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var asset = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(path);
                Assert.That(asset, Is.Not.Null);
                Assert.That(asset.Definition.trackGeometryId, Is.EqualTo("SandboxStraight100m"));
                Assert.That(asset.TryEvaluate(100f, out var sample), Is.True);
                Assert.That(sample.Position.z, Is.EqualTo(100f).Within(0.0001f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase("guideLine")]
        [TestCase("trainGeometry")]
        public void LegacyPreviewAssetReferenceIsPreserved(string legacyFieldName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrackGeometryAsset>(
                "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrackGeometryStraight100m.asset");
            Assert.That(asset, Is.Not.Null);
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/LegacyTrackGeometryPreview.prefab");
            var gameObject = new GameObject("TrackGeometry migration test");
            try
            {
                var preview = gameObject.AddComponent<TrackGeometryPreview>();
                var serialized = new SerializedObject(preview);
                serialized.FindProperty("trackGeometry").objectReferenceValue = asset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
                Object.DestroyImmediate(gameObject);

                string yaml = File.ReadAllText(path).Replace("trackGeometry:", legacyFieldName + ":");
                File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var loadedPreview = prefab.GetComponent<TrackGeometryPreview>();
                Assert.That(loadedPreview, Is.Not.Null);
                serialized = new SerializedObject(loadedPreview);
                Assert.That(serialized.FindProperty("trackGeometry").objectReferenceValue, Is.SameAs(asset));
            }
            finally
            {
                if (gameObject != null) Object.DestroyImmediate(gameObject);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
