using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nakatetsu.Track.Graph.Circuit;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Tests
{
    public sealed class TrackGraphJsonImporterTests
    {
        private string sourceDirectory;
        private string assetDirectory;
        private string manifestPath;
        private const string GeometryJson = @"{
            ""geometries"": [{
                ""trackGeometryId"": ""G"", ""lengthM"": 10,
                ""originRotation"": {""w"": 1},
                ""horizontalSegments"": [{""type"": ""Straight"", ""lengthM"": 10}],
                ""verticalSegments"": []
            }],
            ""nodes"": [
                {""nodeId"": ""A"", ""connectedEdgeIds"": [""E""]},
                {""nodeId"": ""B"", ""connectedEdgeIds"": [""E""]}
            ]
        }";
        private const string EdgeJson = @"{
            ""edges"": [{
                ""edgeId"": ""E"", ""nodeAId"": ""A"", ""nodeBId"": ""B"",
                ""geometryId"": ""G"", ""travelDirection"": 1,
                ""startDistanceOnGeometryM"": 0, ""endDistanceOnGeometryM"": 10,
                ""offsetSegments"": [{""type"": ""Constant"", ""startDistanceOnGeometryM"": 0,
                    ""endDistanceOnGeometryM"": 10, ""offsetM"": 0}]
            }],
            ""circuits"": [{""circuitId"": ""C"", ""controlKind"": ""Interlocking"", ""sections"": [{
                ""edgeId"": ""E"", ""startDistanceOnGeometryM"": 0, ""endDistanceOnGeometryM"": 10
            }]}]
        }";

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            sourceDirectory = Path.Combine(Path.GetTempPath(), "TrackGraphJson-" + id);
            Directory.CreateDirectory(sourceDirectory);
            assetDirectory = "Assets/__TrackGraphJson-" + id;
            manifestPath = Path.Combine(sourceDirectory, "graph.json");
            File.WriteAllText(Path.Combine(sourceDirectory, "station.json"), GeometryJson);
            File.WriteAllText(Path.Combine(sourceDirectory, "section.json"), EdgeJson);
            WriteManifest(new List<string> { "station.json", "section.json" });
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(assetDirectory)) AssetDatabase.DeleteAsset(assetDirectory);
            if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
        }

        [Test]
        public void NakatetsuJsonPreservesAllExistingDefinitions()
        {
            var actual = TrackGraphJsonImporter.Load(TrackGraphJsonImporter.NakatetsuManifestPath, out var manifest);
            var expected = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(manifest.assetPath).Definition;
            Assert.That(actual.graphId, Is.EqualTo(expected.graphId));
            AssertRecords(expected.nodes, actual.nodes, x => x.nodeId);
            AssertRecords(expected.connections, actual.connections, x => x.connectionId);
            // 旧AssetにはcontrolKindがないため、既存の回路形状を比較する。
            var circuits = actual.circuits.ToDictionary(x => x.circuitId);
            Assert.That(circuits.Keys, Is.EquivalentTo(expected.circuits.Select(x => x.circuitId)));
            foreach (var circuit in expected.circuits)
            {
                var other = circuits[circuit.circuitId];
                AssertSegments(circuit.sections, other.sections);
                Assert.That(other.controlKind, Is.EqualTo(TrackControlKind.Block).Or.EqualTo(TrackControlKind.Interlocking), circuit.circuitId);
            }
            Assert.That(circuits["NT01_1RT"].controlKind, Is.EqualTo(TrackControlKind.Interlocking));
            Assert.That(circuits["NT01_NT02_D1T"].controlKind, Is.EqualTo(TrackControlKind.Block));
            var geometries = actual.geometries.ToDictionary(x => x.trackGeometryId);
            Assert.That(geometries.Keys, Is.EquivalentTo(expected.geometries.Select(x => x.trackGeometryId)));
            foreach (var geometry in expected.geometries)
            {
                var other = geometries[geometry.trackGeometryId];
                Assert.That(other.displayName, Is.EqualTo(geometry.displayName));
                Assert.That(other.lengthM, Is.EqualTo(geometry.lengthM));
                Assert.That(other.originPosition, Is.EqualTo(geometry.originPosition));
                Assert.That(other.originRotation, Is.EqualTo(geometry.originRotation));
                AssertSegments(geometry.horizontalSegments, other.horizontalSegments);
                AssertSegments(geometry.verticalSegments, other.verticalSegments);
            }
            var edges = actual.edges.ToDictionary(x => x.edgeId);
            Assert.That(edges.Keys, Is.EquivalentTo(expected.edges.Select(x => x.edgeId)));
            foreach (var edge in expected.edges)
            {
                var other = edges[edge.edgeId];
                Assert.That(other.nodeAId, Is.EqualTo(edge.nodeAId));
                Assert.That(other.nodeBId, Is.EqualTo(edge.nodeBId));
                Assert.That(other.geometryId, Is.EqualTo(edge.geometryId));
                Assert.That(other.travelDirection, Is.EqualTo(edge.travelDirection));
                Assert.That(other.startDistanceOnGeometryM, Is.EqualTo(edge.startDistanceOnGeometryM));
                Assert.That(other.endDistanceOnGeometryM, Is.EqualTo(edge.endDistanceOnGeometryM));
                AssertSegments(edge.offsetSegments, other.offsetSegments);
                Assert.That(other.distanceMap, Is.Empty, "Distance maps must be generated, not stored in source JSON.");
            }
        }

        [Test]
        public void GenerateBuildsDistanceMapsAndPreservesExistingAssetGuid()
        {
            var asset = TrackGraphJsonImporter.Generate(manifestPath);
            Assert.That(asset.Definition.circuits.Single().sections.Single().edgeId, Is.EqualTo("E"));
            Assert.That(asset.Definition.circuits.Single().controlKind, Is.EqualTo(TrackControlKind.Interlocking));
            Assert.That(asset.Definition.edges.Single().LengthM, Is.EqualTo(10f).Within(0.0001f));
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            TrackGraphJsonImporter.Generate(manifestPath);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Resources.UnloadAsset(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(path);
            Assert.That(asset.Definition.circuits.Single().controlKind, Is.EqualTo(TrackControlKind.Interlocking));

            // 不正な入力で生成済みAssetを変更しない。
            string before = File.ReadAllText(path);
            File.WriteAllText(Path.Combine(sourceDirectory, "section.json"), EdgeJson.Replace("\"nodeAId\": \"A\"", "\"nodeAId\": \"missing\""));
            Assert.Throws<InvalidDataException>(() => TrackGraphJsonImporter.Generate(manifestPath));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test]
        public void MissingControlKindInLegacyJsonRemainsUnknown()
        {
            File.WriteAllText(Path.Combine(sourceDirectory, "section.json"),
                EdgeJson.Replace("\"controlKind\": \"Interlocking\", ", ""));
            var graph = TrackGraphJsonImporter.Load(manifestPath, out _);
            Assert.That(graph.circuits.Single().controlKind, Is.EqualTo(TrackControlKind.Unknown));
        }

        [Test]
        public void NakatetsuDistanceMapsCanBeRegenerated()
        {
            string generatedPath = assetDirectory + "/NakatetsuGraph.asset";
            TrackGraphJsonImporter.Generate(TrackGraphJsonImporter.NakatetsuManifestPath, generatedPath);
            AssetDatabase.ImportAsset(generatedPath, ImportAssetOptions.ForceUpdate);
            var graph = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(generatedPath).Definition;
            var errors = new List<string>();
            Assert.That(TrackGraphCompiler.TryCompile(graph, new TrackGraphContext(), errors),
                Is.True, string.Join("\n", errors));
            TrackGraphJsonImporter.Load(TrackGraphJsonImporter.NakatetsuManifestPath, out var manifest);
            var original = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(manifest.assetPath).Definition.edges.ToDictionary(x => x.edgeId);
            foreach (var edge in graph.edges)
            {
                Assert.That(edge.distanceMap.Count, Is.EqualTo(original[edge.edgeId].distanceMap.Count), edge.edgeId);
                Assert.That(edge.LengthM, Is.EqualTo(original[edge.edgeId].LengthM).Within(0.001f), edge.edgeId);
            }
        }

        [TestCase("unknown_segment")]
        [TestCase("duplicate_fragment")]
        [TestCase("duplicate_id")]
        [TestCase("missing_fragment")]
        [TestCase("malformed_json")]
        public void InvalidSourceIsRejected(string failure)
        {
            if (failure == "unknown_segment")
                File.WriteAllText(Path.Combine(sourceDirectory, "station.json"), GeometryJson.Replace("Straight", "Unknown"));
            else if (failure == "duplicate_fragment")
                WriteManifest(new List<string> { "station.json", "section.json", "./section.json" });
            else if (failure == "duplicate_id")
            {
                File.WriteAllText(Path.Combine(sourceDirectory, "other.json"), EdgeJson);
                WriteManifest(new List<string> { "station.json", "section.json", "other.json" });
            }
            else if (failure == "missing_fragment")
                File.Delete(Path.Combine(sourceDirectory, "section.json"));
            else
                File.WriteAllText(Path.Combine(sourceDirectory, "section.json"), "{");
            if (failure == "missing_fragment")
                Assert.Throws<FileNotFoundException>(() => TrackGraphJsonImporter.Load(manifestPath, out _));
            else
                Assert.Throws<InvalidDataException>(() => TrackGraphJsonImporter.Load(manifestPath, out _));
        }

        private void WriteManifest(List<string> files)
        {
            File.WriteAllText(manifestPath, JsonUtility.ToJson(new TrackGraphJsonManifest
            {
                schemaVersion = 1, graphId = "test", assetPath = assetDirectory + "/Graph.asset",
                integrationStepM = 1f, files = files
            }));
        }

        private static void AssertRecords<T>(IEnumerable<T> expected, IEnumerable<T> actual, Func<T, string> key)
        {
            var byId = actual.ToDictionary(key);
            Assert.That(byId.Keys, Is.EquivalentTo(expected.Select(key)));
            foreach (var record in expected)
                Assert.That(JsonUtility.ToJson(byId[key(record)]), Is.EqualTo(JsonUtility.ToJson(record)), key(record));
        }

        private static void AssertSegments<T>(IList<T> expected, IList<T> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].GetType(), Is.EqualTo(expected[index].GetType()));
                Assert.That(JsonUtility.ToJson(actual[index]), Is.EqualTo(JsonUtility.ToJson(expected[index])));
            }
        }
    }
}
