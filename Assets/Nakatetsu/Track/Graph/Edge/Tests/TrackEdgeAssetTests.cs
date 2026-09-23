using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeAssetTests
    {
        [Test]
        public void NewAssetHasDefinitionAndEmptyDistanceMap()
        {
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                Assert.That(asset.Definition, Is.Not.Null);
                Assert.That(asset.Definition.distanceMap, Is.Not.Null.And.Empty);
                Assert.That(asset.Definition.offsetSegments, Is.Not.Null.And.Empty);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SavedAssetPreservesDefinitionAndHiddenDistanceMap()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TrackEdgeSerializationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                asset.name = "TrackEdgeSerializationTest";
                var definition = asset.Definition;
                definition.edgeId = "Edge1";
                definition.nodeAId = "NodeA";
                definition.nodeBId = "NodeB";
                definition.geometryId = "Geometry1";
                definition.startDistanceOnGeometryM = 100f;
                definition.endDistanceOnGeometryM = 25f;
                definition.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
                {
                    startDistanceOnGeometryM = 25f,
                    endDistanceOnGeometryM = 100f,
                    offsetM = -3.5f
                });
                definition.distanceMap.Add(new TrackEdgeDistanceSample
                {
                    distanceOnGeometryM = 100f,
                    distanceOnEdgeM = 0f
                });
                definition.distanceMap.Add(new TrackEdgeDistanceSample
                {
                    distanceOnGeometryM = 25f,
                    distanceOnEdgeM = 75.5f
                });

                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var loaded = AssetDatabase.LoadAssetAtPath<TrackEdgeAsset>(path);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Definition.edgeId, Is.EqualTo("Edge1"));
                Assert.That(loaded.Definition.nodeAId, Is.EqualTo("NodeA"));
                Assert.That(loaded.Definition.nodeBId, Is.EqualTo("NodeB"));
                Assert.That(loaded.Definition.geometryId, Is.EqualTo("Geometry1"));
                Assert.That(loaded.Definition.startDistanceOnGeometryM, Is.EqualTo(100f));
                Assert.That(loaded.Definition.endDistanceOnGeometryM, Is.EqualTo(25f));
                var segments = loaded.Definition.offsetSegments;
                Assert.That(segments, Has.Count.EqualTo(1));
                Assert.That(segments[0], Is.TypeOf<TrackEdgeConstantOffsetSegment>());
                Assert.That(segments[0].startDistanceOnGeometryM, Is.EqualTo(25f));
                Assert.That(segments[0].endDistanceOnGeometryM, Is.EqualTo(100f));
                Assert.That(((TrackEdgeConstantOffsetSegment)segments[0]).offsetM, Is.EqualTo(-3.5f));
                Assert.That(segments[0].EvaluateOffsetM(60f), Is.EqualTo(-3.5f));
                Assert.That(segments[0].EvaluateDerivative(60f), Is.Zero);
                var samples = loaded.Definition.distanceMap;
                Assert.That(samples, Has.Count.EqualTo(2));
                Assert.That(samples[0].distanceOnGeometryM, Is.EqualTo(100f));
                Assert.That(samples[0].distanceOnEdgeM, Is.Zero);
                Assert.That(samples[1].distanceOnGeometryM, Is.EqualTo(25f));
                Assert.That(samples[1].distanceOnEdgeM, Is.EqualTo(75.5f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset))
                    Object.DestroyImmediate(asset);
            }
        }
    }
}
