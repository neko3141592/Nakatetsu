using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nakatetsu.Track.Interlocking.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingJsonImporterTests
    {
        private const string SourceRoot = "SourceAssets/Track/Network/NakatetsuInterlocking";
        private string sourceDirectory;
        private string assetDirectory;
        private string manifestPath;
        private TrackInterlockingJsonManifest manifest;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            sourceDirectory = Path.Combine(Path.GetTempPath(), "InterlockingJson-" + id);
            Directory.CreateDirectory(sourceDirectory);
            assetDirectory = "Assets/__InterlockingJson-" + id;
            manifestPath = Path.Combine(sourceDirectory, "interlocking.json");
            manifest = new TrackInterlockingJsonManifest
            {
                schemaVersion = 1,
                graphManifestPath = ProjectPath("SourceAssets/Track/Network/NakatetsuTrackGraph/graph.json"),
                stations = new List<TrackInterlockingJsonStation>()
            };
            AddStation("NT01");
            WriteManifest();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(assetDirectory)) AssetDatabase.DeleteAsset(assetDirectory);
            if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
        }

        [TestCase("NT01")]
        [TestCase("NT05")]
        [TestCase("NT07")]
        [TestCase("NT11")]
        public void StationJsonPreservesExistingAssetDefinition(string stationId)
        {
            var definition = TrackInterlockingJsonImporter.LoadStation(
                TrackInterlockingJsonImporter.NakatetsuManifestPath, stationId, out var station);
            var asset = AssetDatabase.LoadAssetAtPath<TrackStationInterlockingAsset>(station.assetPath);
            Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(JsonUtility.ToJson(asset.Definition)));
        }

        [Test]
        public void SelectedStationDoesNotReadOtherStations()
        {
            manifest.stations.Add(new TrackInterlockingJsonStation
            {
                stationId = "NT05", file = "missing.json", assetPath = assetDirectory + "/NT05.asset"
            });
            WriteManifest();
            var definition = TrackInterlockingJsonImporter.LoadStation(manifestPath, "NT01", out _);
            Assert.That(definition.interlockingId, Is.EqualTo("NT01"));
            Assert.Throws<FileNotFoundException>(() => TrackInterlockingJsonImporter.LoadAll(manifestPath, out _));
        }

        [Test]
        public void GenerateAllSavesDefinitionsAndPreservesGuids()
        {
            AddStation("NT05");
            AddStation("NT07");
            AddStation("NT11");
            WriteManifest();
            var definitions = TrackInterlockingJsonImporter.LoadAll(manifestPath, out _);
            var assets = TrackInterlockingJsonImporter.GenerateAll(manifestPath);
            Assert.That(assets.Count, Is.EqualTo(4));
            var guids = manifest.stations.ToDictionary(x => x.stationId, x => AssetDatabase.AssetPathToGUID(x.assetPath));
            TrackInterlockingJsonImporter.GenerateAll(manifestPath);
            foreach (var station in manifest.stations)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TrackStationInterlockingAsset>(station.assetPath);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(station.assetPath, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<TrackStationInterlockingAsset>(station.assetPath);
                Assert.That(AssetDatabase.AssetPathToGUID(station.assetPath), Is.EqualTo(guids[station.stationId]));
                Assert.That(JsonUtility.ToJson(asset.Definition), Is.EqualTo(JsonUtility.ToJson(definitions[station.stationId])));
            }
        }

        [Test]
        public void GenerateStationOnlyWritesSelectedStation()
        {
            manifest.stations.Add(new TrackInterlockingJsonStation
            {
                stationId = "NT05", file = "missing.json", assetPath = assetDirectory + "/NT05.asset"
            });
            WriteManifest();
            var asset = TrackInterlockingJsonImporter.GenerateStation(manifestPath, "NT01");
            Assert.That(asset.Definition.interlockingId, Is.EqualTo("NT01"));
            Assert.That(File.Exists(manifest.stations[1].assetPath), Is.False);
        }

        [Test]
        public void InvalidLaterStationDoesNotPartiallyUpdateAssets()
        {
            AddStation("NT05");
            WriteManifest();
            TrackInterlockingJsonImporter.GenerateAll(manifestPath);
            var before = manifest.stations.ToDictionary(x => x.stationId, x => File.ReadAllText(x.assetPath));
            string firstPath = Path.Combine(sourceDirectory, "NT01.json");
            var first = JsonUtility.FromJson<TrackStationInterlockingDefinition>(File.ReadAllText(firstPath));
            first.destinations[0].displayName = "Changed name";
            File.WriteAllText(firstPath, JsonUtility.ToJson(first));
            File.WriteAllText(Path.Combine(sourceDirectory, "NT05.json"), "{");
            Assert.Throws<InvalidDataException>(() => TrackInterlockingJsonImporter.GenerateAll(manifestPath));
            foreach (var station in manifest.stations)
                Assert.That(File.ReadAllText(station.assetPath), Is.EqualTo(before[station.stationId]));
        }

        [TestCase("station_id")]
        [TestCase("unknown_circuit")]
        [TestCase("unknown_connection")]
        [TestCase("duplicate_route")]
        [TestCase("duplicate_station")]
        [TestCase("malformed_json")]
        public void InvalidSourceIsRejected(string failure)
        {
            string path = Path.Combine(sourceDirectory, "NT01.json");
            var definition = JsonUtility.FromJson<TrackStationInterlockingDefinition>(File.ReadAllText(path));
            if (failure == "station_id") definition.interlockingId = "NT99";
            else if (failure == "unknown_circuit") definition.memberTrackCircuitIds.Add("missing");
            else if (failure == "unknown_connection") definition.memberConnectionIds.Add("missing");
            else if (failure == "duplicate_route") definition.routes.Add(definition.routes[0]);
            else if (failure == "duplicate_station")
            {
                manifest.stations.Add(manifest.stations[0]);
                WriteManifest();
            }
            File.WriteAllText(path, failure == "malformed_json" ? "{" : JsonUtility.ToJson(definition));
            Assert.Throws<InvalidDataException>(() => TrackInterlockingJsonImporter.LoadStation(manifestPath, "NT01", out _));
        }

        private void AddStation(string stationId)
        {
            File.Copy(ProjectPath(SourceRoot + "/Stations/" + stationId + ".json"),
                Path.Combine(sourceDirectory, stationId + ".json"));
            manifest.stations.Add(new TrackInterlockingJsonStation
            {
                stationId = stationId, file = stationId + ".json", assetPath = assetDirectory + "/" + stationId + ".asset"
            });
        }

        private void WriteManifest() => File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest));

        private static string ProjectPath(string path) => Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
    }
}
