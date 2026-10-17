using System;
using System.Collections.Generic;
using System.IO;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Editor
{
    [Serializable]
    public sealed class TrackInterlockingJsonStation
    {
        public string stationId;
        public string file;
        public string assetPath;
    }

    [Serializable]
    public sealed class TrackInterlockingJsonManifest
    {
        public int schemaVersion;
        public string graphManifestPath;
        public List<TrackInterlockingJsonStation> stations;
    }

    public static class TrackInterlockingJsonImporter
    {
        public const string NakatetsuManifestPath = "SourceAssets/Track/Network/NakatetsuInterlocking/interlocking.json";

        // 指定駅のJSONだけを読み込む。他駅のJSONが未配置でも使用できる。
        public static TrackStationInterlockingDefinition LoadStation(string manifestPath, string stationId,
            out TrackInterlockingJsonStation station)
        {
            string path = FullPath(manifestPath);
            var manifest = LoadManifest(path);
            station = manifest.stations.Find(x => x.stationId == stationId)
                ?? throw new InvalidDataException("Unknown interlocking station: " + stationId);
            var graph = LoadGraph(path, manifest);
            return ReadStation(path, station, graph);
        }

        public static Dictionary<string, TrackStationInterlockingDefinition> LoadAll(string manifestPath,
            out TrackInterlockingJsonManifest manifest)
        {
            string path = FullPath(manifestPath);
            manifest = LoadManifest(path);
            var graph = LoadGraph(path, manifest);
            var definitions = new Dictionary<string, TrackStationInterlockingDefinition>();
            foreach (var station in manifest.stations)
                definitions.Add(station.stationId, ReadStation(path, station, graph));
            return definitions;
        }

        public static TrackStationInterlockingAsset GenerateStation(string manifestPath, string stationId,
            string assetPath = null)
        {
            RequireEditMode();
            var definition = LoadStation(manifestPath, stationId, out var station);
            return Save(definition, assetPath ?? station.assetPath);
        }

        public static List<TrackStationInterlockingAsset> GenerateAll(string manifestPath)
        {
            RequireEditMode();
            // 全駅の入力と出力先を確認してからAssetを更新する。
            var definitions = LoadAll(manifestPath, out var manifest);
            foreach (var station in manifest.stations) ValidateTarget(station.assetPath);
            var assets = new List<TrackStationInterlockingAsset>();
            foreach (var station in manifest.stations)
                assets.Add(Save(definitions[station.stationId], station.assetPath));
            return assets;
        }

        [MenuItem("Nakatetsu/Track/Generate Interlocking from JSON/All Stations")]
        public static void GenerateNakatetsu()
        {
            var assets = GenerateAll(NakatetsuManifestPath);
            Debug.Log($"Generated {assets.Count} interlocking assets from JSON.");
        }

        [MenuItem("Nakatetsu/Track/Generate Interlocking from JSON/Select Station...")]
        public static void GenerateSelectedStation()
        {
            string path = FullPath(NakatetsuManifestPath);
            var manifest = LoadManifest(path);
            string directory = Path.GetDirectoryName(path);
            string selected = EditorUtility.OpenFilePanel("Select interlocking station JSON",
                Path.Combine(directory, "Stations"), "json");
            if (string.IsNullOrEmpty(selected)) return;
            var station = manifest.stations.Find(x =>
                Path.GetFullPath(Path.Combine(directory, x.file)) == Path.GetFullPath(selected));
            if (station == null) throw new InvalidDataException("Selected JSON is not registered in " + path);
            var asset = GenerateStation(path, station.stationId);
            Debug.Log("Interlocking generated from JSON: " + AssetDatabase.GetAssetPath(asset), asset);
        }

        private static TrackInterlockingJsonManifest LoadManifest(string path)
        {
            var manifest = Read<TrackInterlockingJsonManifest>(path);
            if (manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.graphManifestPath)
                || manifest.stations == null || manifest.stations.Count == 0)
                throw new InvalidDataException("Invalid interlocking manifest: " + path);
            var stationIds = new HashSet<string>(StringComparer.Ordinal);
            var files = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var station in manifest.stations)
            {
                if (station == null || string.IsNullOrWhiteSpace(station.stationId)
                    || string.IsNullOrWhiteSpace(station.file) || string.IsNullOrWhiteSpace(station.assetPath)
                    || !stationIds.Add(station.stationId)
                    || !files.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), station.file)))
                    || !targets.Add(FullPath(station.assetPath)))
                    throw new InvalidDataException("Invalid or duplicate station entry in " + path);
            }
            return manifest;
        }

        private static TrackGraphContext LoadGraph(string path, TrackInterlockingJsonManifest manifest)
        {
            var definition = TrackGraphJsonImporter.Load(
                Path.Combine(Path.GetDirectoryName(path), manifest.graphManifestPath), out _);
            var graph = new TrackGraphContext();
            if (!graph.TryBuildLookups(definition, out string error)) throw new InvalidDataException(error);
            return graph;
        }

        private static TrackStationInterlockingDefinition ReadStation(string manifestPath,
            TrackInterlockingJsonStation station, TrackGraphContext graph)
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath), station.file));
            var definition = Read<TrackStationInterlockingDefinition>(path);
            if (definition.interlockingId != station.stationId)
                throw new InvalidDataException("Station ID does not match manifest: " + path);
            if (!TrackStationInterlockingLogic.TryInitialize(new TrackStationInterlockingContext(), definition, out string error))
                throw new InvalidDataException(path + "\n" + error);
            var circuits = new HashSet<string>();
            foreach (string circuitId in definition.memberTrackCircuitIds)
                if (!circuits.Add(circuitId) || !graph.TryGetCircuit(circuitId, out _))
                    throw new InvalidDataException($"{path}: Unknown or duplicate circuit '{circuitId}'.");
            var connections = new HashSet<string>();
            foreach (string connectionId in definition.memberConnectionIds)
                if (!connections.Add(connectionId) || !graph.TryGetConnection(connectionId, out _))
                    throw new InvalidDataException($"{path}: Unknown or duplicate connection '{connectionId}'.");
            return definition;
        }

        private static TrackStationInterlockingAsset Save(TrackStationInterlockingDefinition definition, string path)
        {
            ValidateTarget(path);
            var asset = AssetDatabase.LoadAssetAtPath<TrackStationInterlockingAsset>(path);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path)));
                AssetDatabase.Refresh();
                asset = ScriptableObject.CreateInstance<TrackStationInterlockingAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            Undo.RegisterCompleteObjectUndo(asset, "Generate Interlocking from JSON");
            var target = asset.Definition;
            target.interlockingId = definition.interlockingId;
            target.memberTrackCircuitIds = definition.memberTrackCircuitIds;
            target.memberConnectionIds = definition.memberConnectionIds;
            target.destinations = definition.destinations;
            target.routes = definition.routes;
            target.turnoutLocks = definition.turnoutLocks;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static void ValidateTarget(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidDataException("Output must be an .asset path inside Assets.");
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null && File.Exists(FullPath(path)))
                throw new InvalidDataException("Output already contains an unreadable asset: " + path);
            if (existing != null && !(existing is TrackStationInterlockingAsset))
                throw new InvalidDataException("Output already contains a different asset type: " + path);
            if (existing != null && !AssetDatabase.IsOpenForEdit(existing))
                throw new IOException("Interlocking asset is not writable: " + path);
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate interlocking assets in Edit Mode.");
        }

        private static string FullPath(string path)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));

        private static T Read<T>(string path) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path),
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None })
                    ?? throw new InvalidDataException("JSON document is null: " + path);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid interlocking JSON: " + path + "\n" + exception.Message, exception);
            }
        }
    }
}
