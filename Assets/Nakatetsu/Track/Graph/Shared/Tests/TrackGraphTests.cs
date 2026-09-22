using System.Collections.Generic;
using System.IO;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Tests
{
    public sealed class TrackGraphTests
    {
        [Test]
        public void SharedSampleIsAccessibleFromGraphAndPreservesScriptGuid()
        {
            Assert.That(TrackGeometryCalculator.TryEvaluate(CreateGraph().geometries[0], 25f, out TrackSample sample), Is.True);
            Assert.That(sample.DistanceM, Is.EqualTo(25f));
            Assert.That(sample.Position, Is.EqualTo(new Vector3(0f, 0f, 25f)));
            Assert.That(sample.Tangent, Is.EqualTo(Vector3.forward));
            Assert.That(sample.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(sample.GradientPermille, Is.Zero);
            Assert.That(typeof(TrackSample).Assembly.GetName().Name, Is.EqualTo("Nakatetsu.Track.Shared"));
            Assert.That(AssetDatabase.GUIDToAssetPath("8768062a803e497687dc0076446dce5d"),
                Is.EqualTo("Assets/Nakatetsu/Track/Shared/Scripts/TrackSample.cs"));
        }

        private static TrackGraphDefinition CreateGraph()
        {
            var graph = new TrackGraphDefinition { graphId = "Graph1" };
            graph.geometries.Add(new TrackGeometryDefinition
            {
                trackGeometryId = "Geometry1",
                lengthM = 100f,
                horizontalSegments = new List<TrackGeometryHorizontalSegment>
                {
                    new TrackGeometryStraightSegment { lengthM = 100f }
                }
            });
            graph.nodes.Add(new TrackNodeDefinition { nodeId = "A", connectedEdgeIds = new List<string> { "Edge1" } });
            graph.nodes.Add(new TrackNodeDefinition { nodeId = "B", connectedEdgeIds = new List<string> { "Edge1" } });
            graph.edges.Add(new TrackEdgeDefinition
            {
                edgeId = "Edge1",
                nodeAId = "A",
                nodeBId = "B",
                geometryId = "Geometry1",
                startDistanceOnGeometryM = 100f,
                endDistanceOnGeometryM = 25f
            });
            return graph;
        }

        [Test]
        public void CompilesCanonicalDefinitionsAndResolvesAllIds()
        {
            var graph = CreateGraph();
            var context = new TrackGraphContext();
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryCompile(graph, context, errors), Is.True, string.Join("; ", errors));
            Assert.That(errors, Is.Empty);
            Assert.That(context.TryGetEdge("Edge1", out var edge), Is.True);
            Assert.That(edge, Is.SameAs(graph.edges[0]));
            Assert.That(context.TryGetNode("A", out var node), Is.True);
            Assert.That(node, Is.SameAs(graph.nodes[0]));
            Assert.That(context.TryGetGeometry(edge.geometryId, out var geometry), Is.True);
            Assert.That(geometry, Is.SameAs(graph.geometries[0]));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        public void UnknownIdsReturnFalse(string id)
        {
            var context = new TrackGraphContext();
            Assert.That(TrackGraphCompiler.TryCompile(CreateGraph(), context, new List<string>()), Is.True);
            Assert.That(context.TryGetEdge(id, out _), Is.False);
            Assert.That(context.TryGetNode(id, out _), Is.False);
            Assert.That(context.TryGetGeometry(id, out _), Is.False);
        }

        [TestCase("missingGeometry", "missing geometry")]
        [TestCase("duplicateGeometry", "Duplicate geometryId")]
        [TestCase("nullGeometries", "geometries are null")]
        [TestCase("invalidGeometryLength", "invalid length")]
        [TestCase("missingNode", "missing nodeA")]
        [TestCase("nullConnectedEdgeId", "empty edgeId")]
        public void InvalidReferencesReturnDiagnostics(string mutation, string expected)
        {
            var graph = CreateGraph();
            switch (mutation)
            {
                case "missingGeometry": graph.edges[0].geometryId = "missing"; break;
                case "duplicateGeometry": graph.geometries.Add(graph.geometries[0]); break;
                case "nullGeometries": graph.geometries = null; break;
                case "invalidGeometryLength": graph.geometries[0].lengthM = float.NaN; break;
                case "missingNode": graph.edges[0].nodeAId = "missing"; break;
                case "nullConnectedEdgeId": graph.nodes[0].connectedEdgeIds.Add(null); break;
            }
            var errors = new List<string>();
            var context = new TrackGraphContext();
            Assert.That(TrackGraphCompiler.TryCompile(graph, context, errors), Is.False);
            Assert.That(string.Join("; ", errors), Does.Contain(expected));
            Assert.That(context.TryGetEdge("Edge1", out _), Is.False);
        }

        [TestCase(-1f, 50f)]
        [TestCase(0f, 101f)]
        [TestCase(25f, 25f)]
        [TestCase(float.NaN, 50f)]
        [TestCase(0f, float.PositiveInfinity)]
        public void InvalidGeometryRangeIsRejected(float start, float end)
        {
            var graph = CreateGraph();
            graph.edges[0].startDistanceOnGeometryM = start;
            graph.edges[0].endDistanceOnGeometryM = end;
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryCompile(graph, new TrackGraphContext(), errors), Is.False);
            Assert.That(string.Join("; ", errors), Does.Contain("distance range"));
        }

        [Test]
        public void SuccessfulRebuildReplacesPreviousIdLookups()
        {
            var context = new TrackGraphContext();
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryCompile(CreateGraph(), context, errors), Is.True);
            Assert.That(TrackGraphCompiler.TryCompile(new TrackGraphDefinition(), context, errors), Is.True);
            Assert.That(context.TryGetEdge("Edge1", out _), Is.False);
            Assert.That(context.TryGetNode("A", out _), Is.False);
            Assert.That(context.TryGetGeometry("Geometry1", out _), Is.False);
        }

        [TestCase("trackGeometryId", "startDistanceOnTrackGeometryM", "endDistanceOnTrackGeometryM")]
        [TestCase("trainGeometryId", "startDistanceOnTrainGeometryM", "endDistanceOnTrainGeometryM")]
        [TestCase("guideLineId", "startDistanceOnGuideLineM", "endDistanceOnGuideLineM")]
        public void GraphAssetPreservesDefinitionsAndLegacyEdgeFields(string oldId, string oldStart, string oldEnd)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TrackGraphMigrationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                asset.name = "TrackGraphMigrationTest";
                var source = CreateGraph();
                source.edges[0].offsetSegments.Add(new TrackEdgeConstantOffsetSegment
                {
                    startDistanceOnGeometryM = 25f, endDistanceOnGeometryM = 100f, offsetM = 3f
                });
                source.edges[0].distanceMap.Add(new TrackEdgeDistanceSample
                {
                    distanceOnGeometryM = 100f, distanceOnEdgeM = 0f
                });
                asset.Definition.graphId = source.graphId;
                asset.Definition.geometries.AddRange(source.geometries);
                asset.Definition.nodes.AddRange(source.nodes);
                asset.Definition.edges.AddRange(source.edges);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;

                string yaml = File.ReadAllText(path)
                    .Replace("geometryId:", oldId + ":")
                    .Replace("startDistanceOnGeometryM:", oldStart + ":")
                    .Replace("endDistanceOnGeometryM:", oldEnd + ":");
                // Offset segments did not use the old Edge field names.
                int referencesIndex = yaml.IndexOf("  references:", System.StringComparison.Ordinal);
                if (referencesIndex >= 0)
                    yaml = yaml.Substring(0, referencesIndex) + yaml.Substring(referencesIndex)
                        .Replace(oldStart + ":", "startDistanceOnGeometryM:")
                        .Replace(oldEnd + ":", "endDistanceOnGeometryM:");
                File.WriteAllText(path, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var loaded = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(path);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Definition.graphId, Is.EqualTo("Graph1"));
                Assert.That(loaded.Definition.nodes, Has.Count.EqualTo(2));
                Assert.That(loaded.Definition.geometries, Has.Count.EqualTo(1));
                Assert.That(loaded.Definition.edges, Has.Count.EqualTo(1));
                var edge = loaded.Definition.edges[0];
                Assert.That(edge.geometryId, Is.EqualTo("Geometry1"));
                Assert.That(edge.startDistanceOnGeometryM, Is.EqualTo(100f));
                Assert.That(edge.endDistanceOnGeometryM, Is.EqualTo(25f));
                Assert.That(edge.offsetSegments[0], Is.TypeOf<TrackEdgeConstantOffsetSegment>());
                Assert.That(edge.offsetSegments[0].startDistanceOnGeometryM, Is.EqualTo(25f));
                Assert.That(edge.offsetSegments[0].endDistanceOnGeometryM, Is.EqualTo(100f));
                Assert.That(edge.offsetSegments[0].EvaluateOffsetM(50f), Is.EqualTo(3f));
                Assert.That(edge.distanceMap, Has.Count.EqualTo(1));
                Assert.That(edge.distanceMap[0].distanceOnGeometryM, Is.EqualTo(100f));
                var errors = new List<string>();
                Assert.That(TrackGraphCompiler.TryCompile(loaded.Definition, new TrackGraphContext(), errors),
                    Is.True, string.Join("; ", errors));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }
    }
}
