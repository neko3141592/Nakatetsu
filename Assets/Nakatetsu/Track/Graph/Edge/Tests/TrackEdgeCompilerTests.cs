using System.Collections.Generic;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;
using Nakatetsu.Track.Graph.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeCompilerTests
    {
        private static TrackGraphDefinition Create(bool reverse = false, bool curve = false)
        {
            var graph = new TrackGraphDefinition();
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = 10f };
            geometry.horizontalSegments.Add(curve
                ? (TrackGeometryHorizontalSegment)new TrackGeometryCircularSegment { lengthM = 10f, radiusM = 50f }
                : new TrackGeometryStraightSegment { lengthM = 10f });
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment { lengthM = 10f, gradientPermille = 20f });
            graph.geometries.Add(geometry);
            graph.nodes.Add(new TrackNodeDefinition { nodeId = "A", connectedEdgeIds = new List<string> { "E" } });
            graph.nodes.Add(new TrackNodeDefinition { nodeId = "B", connectedEdgeIds = new List<string> { "E" } });
            var edge = new TrackEdgeDefinition
            { edgeId = "E", geometryId = "G", nodeAId = "A", nodeBId = "B",
                startDistanceOnGeometryM = reverse ? 10f : 0f, endDistanceOnGeometryM = reverse ? 0f : 10f };
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 10f, offsetM = 2f });
            graph.edges.Add(edge);
            return graph;
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void BuildsMonotonicMapAndRuntimeCanEvaluate(bool reverse, bool curve)
        {
            var graph = Create(reverse, curve);
            var edge = graph.edges[0];
            var original = edge.distanceMap;
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryBuildDistanceMaps(graph, 0.05f, out var maps, errors), Is.True, string.Join(";", errors));
            Assert.That(edge.distanceMap, Is.SameAs(original));
            var map = maps["E"];
            Assert.That(map[0].distanceOnEdgeM, Is.Zero);
            Assert.That(map[0].distanceOnGeometryM, Is.EqualTo(edge.startDistanceOnGeometryM));
            Assert.That(map[map.Count - 1].distanceOnGeometryM, Is.EqualTo(edge.endDistanceOnGeometryM));
            for (int i = 1; i < map.Count; i++)
            {
                Assert.That(map[i].distanceOnEdgeM, Is.GreaterThan(map[i - 1].distanceOnEdgeM));
                Assert.That((map[i].distanceOnGeometryM - map[i - 1].distanceOnGeometryM) * (reverse ? -1f : 1f), Is.GreaterThan(0f));
            }
            float rate = curve ? 0.96f : 1f;
            float expectedLength = 10f * Mathf.Sqrt(rate * rate + 0.02f * 0.02f);
            Assert.That(map[map.Count - 1].distanceOnEdgeM, Is.EqualTo(expectedLength).Within(0.0001f));
            edge.distanceMap = map;
            var context = new TrackGraphContext();
            Assert.That(context.TryBuildLookups(graph, out _), Is.True);
            Assert.That(TrackEdgeCalculator.TryEvaluate(context, "E", expectedLength / 2f, out var sample), Is.True);
            Assert.That(sample.Position.y, Is.EqualTo(0.1f).Within(0.00001f));
        }

        [Test]
        public void IncludesOffsetAndGeometryBoundariesAndShortFinalStep()
        {
            var graph = Create();
            var edge = graph.edges[0];
            edge.endDistanceOnGeometryM = 0.113f;
            edge.offsetSegments[0].endDistanceOnGeometryM = 0.037f;
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = 0.037f, endDistanceOnGeometryM = 10f, offsetM = 2f });
            graph.geometries[0].horizontalSegments[0].lengthM = 0.073f;
            graph.geometries[0].horizontalSegments.Add(new TrackGeometryStraightSegment
            { startDistanceM = 0.073f, lengthM = 10f - 0.073f });
            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(edge, graph.geometries[0], 0.05f, out var map, out var error), Is.True, error);
            Assert.That(map.Exists(s => s.distanceOnGeometryM == 0.037f), Is.True);
            Assert.That(map.Exists(s => s.distanceOnGeometryM == 0.073f), Is.True);
            Assert.That(map[map.Count - 1].distanceOnGeometryM, Is.EqualTo(0.113f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.00000001f)]
        public void InvalidOrExcessiveStepFailsWithoutOutput(float step)
        {
            var graph = Create();
            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(graph.edges[0], graph.geometries[0], step, out var map, out var error), Is.False);
            Assert.That(map, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingCoverageOrOffsetJumpFails(bool jump)
        {
            var graph = Create();
            graph.edges[0].offsetSegments[0].endDistanceOnGeometryM = 4f;
            graph.edges[0].offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            { startDistanceOnGeometryM = jump ? 4f : 5f, endDistanceOnGeometryM = 10f, offsetM = jump ? 3f : 2f });
            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(graph.edges[0], graph.geometries[0], 0.05f, out var map, out _), Is.False);
            Assert.That(map, Is.Null);
        }

        [Test]
        public void LaterEdgeFailureDoesNotPublishPartialMaps()
        {
            var graph = Create();
            var first = graph.edges[0];
            first.distanceMap.Add(new TrackEdgeDistanceSample { distanceOnGeometryM = 123f, distanceOnEdgeM = 456f });
            var oldMap = first.distanceMap;
            var second = new TrackEdgeDefinition
            { edgeId = "Broken", geometryId = "G", nodeAId = "A", nodeBId = "B", endDistanceOnGeometryM = 10f };
            graph.edges.Add(second);
            foreach (var node in graph.nodes) node.connectedEdgeIds.Add("Broken");
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryBuildDistanceMaps(graph, 0.05f, out var maps, errors), Is.False);
            Assert.That(maps, Is.Null);
            Assert.That(first.distanceMap, Is.SameAs(oldMap));
            Assert.That(first.distanceMap[0].distanceOnEdgeM, Is.EqualTo(456f));
            Assert.That(string.Join(";", errors), Does.Contain("Broken"));
        }

        [Test]
        public void EditorCompileSavesMapsAndFailedRecompilePreservesThem()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/GraphDistanceCompileTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                var graph = Create();
                asset.Definition.edges.AddRange(graph.edges);
                asset.Definition.nodes.AddRange(graph.nodes);
                asset.Definition.geometries.AddRange(graph.geometries);
                AssetDatabase.CreateAsset(asset, path);
                Assert.That(TrackGraphAssetEditor.TryCompileAndSave(asset, 0.05f, out var error), Is.True, error);
                var before = asset.Definition.edges[0].distanceMap;
                Assert.That(TrackGraphAssetEditor.TryCompileAndSave(asset, 0f, out _), Is.False);
                Assert.That(asset.Definition.edges[0].distanceMap, Is.SameAs(before));
                Undo.ClearUndo(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(path);
                Assert.That(loaded.Definition.edges[0].distanceMap.Count, Is.GreaterThan(2));
                Assert.That(loaded.Definition.edges[0].TryConvertToGeometryDistance(5f, out _), Is.True);
            }
            finally
            {
                if (asset != null) Undo.ClearUndo(asset);
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }
    }
}
