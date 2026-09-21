using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Nakatetsu.Track.TrainGeometry;

namespace Nakatetsu.Track.Tests
{
    public sealed class TrainGeometryAssetTests
    {
        [Test]
        public void IncludedStraightAssetLoadsAndEvaluates()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrainGeometryAsset>(
                "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrainGeometryStraight100m.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Definition.trainGeometryId, Is.EqualTo("SandboxStraight100m"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)),
                Is.EqualTo("8d0abc7bf4be4bdfbc7d7d66a15fe788"));
            Assert.That(asset.TryEvaluate(100f, out var sample), Is.True);
            Assert.That(Vector3.Distance(sample.Position, new Vector3(0, 0, 100)), Is.LessThan(0.0001f));
        }

        [Test]
        public void LegacyDefinitionIdIsPreserved()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/LegacyTrainGeometry.asset");
            try
            {
                string yaml = File.ReadAllText(
                    "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrainGeometryStraight100m.asset");
                File.WriteAllText(path, yaml.Replace("trainGeometryId:", "guideLineId:"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var asset = AssetDatabase.LoadAssetAtPath<TrainGeometryAsset>(path);
                Assert.That(asset, Is.Not.Null);
                Assert.That(asset.Definition.trainGeometryId, Is.EqualTo("SandboxStraight100m"));
                Assert.That(asset.TryEvaluate(100f, out var sample), Is.True);
                Assert.That(sample.Position.z, Is.EqualTo(100f).Within(0.0001f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void LegacyPreviewAssetReferenceIsPreserved()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrainGeometryAsset>(
                "Assets/Nakatetsu/Track/Graph/Geometry/Data/TrainGeometryStraight100m.asset");
            Assert.That(asset, Is.Not.Null);
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/LegacyTrainGeometryPreview.prefab");
            var gameObject = new GameObject("TrainGeometry migration test");
            try
            {
                var preview = gameObject.AddComponent<TrainGeometryPreview>();
                var serialized = new SerializedObject(preview);
                serialized.FindProperty("trainGeometry").objectReferenceValue = asset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
                Object.DestroyImmediate(gameObject);

                string yaml = File.ReadAllText(path).Replace("trainGeometry:", "guideLine:");
                File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var loadedPreview = prefab.GetComponent<TrainGeometryPreview>();
                Assert.That(loadedPreview, Is.Not.Null);
                serialized = new SerializedObject(loadedPreview);
                Assert.That(serialized.FindProperty("trainGeometry").objectReferenceValue, Is.SameAs(asset));
            }
            finally
            {
                if (gameObject != null) Object.DestroyImmediate(gameObject);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
