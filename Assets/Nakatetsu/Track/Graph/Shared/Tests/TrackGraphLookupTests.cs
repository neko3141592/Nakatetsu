using System.Reflection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Tests
{
    public sealed class TrackGraphLookupTests
    {
        private static TrackGraphDefinition CreateDefinition()
        {
            // Intentionally incomplete authoring data: indexing must not require compiled geometry or a LUT.
            var definition = new TrackGraphDefinition();
            definition.nodes.Add(new TrackNodeDefinition { nodeId = "Node1" });
            definition.edges.Add(new TrackEdgeDefinition { edgeId = "Edge1" });
            definition.geometries.Add(new TrackGeometryDefinition { trackGeometryId = "Geometry1" });
            return definition;
        }

        [Test]
        public void BuildsAllLookupsWithoutCompilingGeometry()
        {
            var definition = CreateDefinition();
            var context = new TrackGraphContext();
            Assert.That(context.IsInitialized, Is.False);
            Assert.That(context.TryBuildLookups(definition, out var error), Is.True, error);
            Assert.That(context.IsInitialized, Is.True);
            Assert.That(context.TryGetNode("Node1", out var node), Is.True);
            Assert.That(node, Is.SameAs(definition.nodes[0]));
            Assert.That(context.TryGetEdge("Edge1", out var edge), Is.True);
            Assert.That(edge, Is.SameAs(definition.edges[0]));
            Assert.That(context.TryGetGeometry("Geometry1", out var geometry), Is.True);
            Assert.That(geometry, Is.SameAs(definition.geometries[0]));
            Assert.That(edge.distanceMap, Is.Empty);
            Assert.That(geometry.lengthM, Is.Zero);
        }

        [TestCase("duplicateNode")]
        [TestCase("duplicateEdge")]
        [TestCase("duplicateGeometry")]
        [TestCase("emptyId")]
        [TestCase("nullEntry")]
        [TestCase("nullList")]
        [TestCase("nullDefinition")]
        public void FailedBuildClearsOldAndPartialLookups(string mutation)
        {
            var context = new TrackGraphContext();
            var definition = CreateDefinition();
            Assert.That(context.TryBuildLookups(definition, out _), Is.True);
            switch (mutation)
            {
                case "duplicateNode": definition.nodes.Add(definition.nodes[0]); break;
                case "duplicateEdge": definition.edges.Add(definition.edges[0]); break;
                case "duplicateGeometry": definition.geometries.Add(definition.geometries[0]); break;
                case "emptyId": definition.edges[0].edgeId = " "; break;
                case "nullEntry": definition.geometries.Add(null); break;
                case "nullList": definition.geometries = null; break;
                case "nullDefinition": definition = null; break;
            }
            Assert.That(context.TryBuildLookups(definition, out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(context.IsInitialized, Is.False);
            Assert.That(context.TryGetNode("Node1", out _), Is.False);
            Assert.That(context.TryGetEdge("Edge1", out _), Is.False);
            Assert.That(context.TryGetGeometry("Geometry1", out _), Is.False);
        }

        [Test]
        public void ControllerAwakeBuildsLookupsAndMissingAssetResetsThem()
        {
            var gameObject = new GameObject("Graph lookup test");
            gameObject.SetActive(false);
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                var source = CreateDefinition();
                asset.Definition.nodes.AddRange(source.nodes);
                asset.Definition.edges.AddRange(source.edges);
                asset.Definition.geometries.AddRange(source.geometries);
                var controller = gameObject.AddComponent<TrackGraphController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("graphAsset").objectReferenceValue = asset;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                typeof(TrackGraphController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(controller.IsInitialized, Is.True);
                Assert.That(controller.Context.TryGetEdge("Edge1", out _), Is.True);

                serialized.Update();
                serialized.FindProperty("graphAsset").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(controller.TryInitialize(out var error), Is.False);
                Assert.That(error, Does.Contain("not assigned"));
                Assert.That(controller.IsInitialized, Is.False);
                Assert.That(controller.Context.TryGetEdge("Edge1", out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
