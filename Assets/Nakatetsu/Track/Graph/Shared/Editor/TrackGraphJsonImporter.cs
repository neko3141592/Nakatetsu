using System;
using System.Collections.Generic;
using System.IO;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Editor
{
    [Serializable]
    public sealed class TrackGraphJsonManifest
    {
        public int schemaVersion;
        public string graphId;
        public string assetPath;
        public float integrationStepM;
        public List<string> files;
    }

    public static class TrackGraphJsonImporter
    {
        public const string NakatetsuManifestPath = "SourceAssets/Track/Network/NakatetsuTrackGraph/graph.json";

        // 全ファイルを結合してから、ファイルをまたぐID参照を検証する。
        public static TrackGraphDefinition Load(string manifestPath, out TrackGraphJsonManifest manifest)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.GetFullPath(Path.Combine(root, manifestPath));
            manifest = Read<TrackGraphJsonManifest>(fullPath);
            if (manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.graphId)
                || manifest.files == null || manifest.files.Count == 0)
                throw new InvalidDataException("Invalid TrackGraph JSON manifest: " + fullPath);

            var graph = new TrackGraphDefinition { graphId = manifest.graphId };
            var loaded = new HashSet<string>(StringComparer.Ordinal);
            string directory = Path.GetDirectoryName(fullPath);
            foreach (string file in manifest.files)
            {
                if (string.IsNullOrWhiteSpace(file))
                    throw new InvalidDataException("Empty fragment path in " + fullPath);
                string fragmentPath = Path.GetFullPath(Path.Combine(directory, file));
                if (!loaded.Add(fragmentPath))
                    throw new InvalidDataException("Duplicate fragment: " + fragmentPath);
                var fragment = Read<TrackGraphDefinition>(fragmentPath);
                if (!string.IsNullOrEmpty(fragment.graphId) && fragment.graphId != graph.graphId)
                    throw new InvalidDataException("Mismatched graphId in " + fragmentPath);
                if (fragment.geometries == null || fragment.nodes == null || fragment.edges == null
                    || fragment.connections == null || fragment.circuits == null)
                    throw new InvalidDataException("Graph lists must not be null in " + fragmentPath);
                graph.geometries.AddRange(fragment.geometries);
                graph.nodes.AddRange(fragment.nodes);
                graph.edges.AddRange(fragment.edges);
                graph.connections.AddRange(fragment.connections);
                graph.circuits.AddRange(fragment.circuits);
            }

            var errors = new List<string>();
            if (!TrackGraphCompiler.TryCompile(graph, new TrackGraphContext(), errors))
                throw new InvalidDataException(string.Join("\n", errors));
            return graph;
        }

        // 検証と距離表の生成が成功してから、既存Assetの中身だけを更新する。GUIDは維持する。
        public static TrackGraphAsset Generate(string manifestPath, string assetPath = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate TrackGraph assets in Edit Mode.");
            var definition = Load(manifestPath, out var manifest);
            assetPath ??= manifest.assetPath;
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".asset", StringComparison.Ordinal) || assetPath.Contains(".."))
                throw new InvalidDataException("Output must be an .asset path inside Assets.");

            var errors = new List<string>();
            if (!TrackGraphCompiler.TryBuildDistanceMaps(definition, manifest.integrationStepM, out var maps, errors))
                throw new InvalidDataException(string.Join("\n", errors));
            foreach (var edge in definition.edges)
                edge.distanceMap = maps[edge.edgeId];

            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null && !(existing is TrackGraphAsset))
                throw new InvalidDataException("Output already contains a different asset type: " + assetPath);
            var asset = existing as TrackGraphAsset;
            if (asset != null && !AssetDatabase.IsOpenForEdit(asset))
                throw new IOException("TrackGraphAsset is not writable: " + assetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                AssetDatabase.Refresh();
                asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            Undo.RegisterCompleteObjectUndo(asset, "Generate Track Graph from JSON");
            var target = asset.Definition;
            target.graphId = definition.graphId;
            target.geometries = definition.geometries;
            target.nodes = definition.nodes;
            target.edges = definition.edges;
            target.connections = definition.connections;
            target.circuits = definition.circuits;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        [MenuItem("Nakatetsu/Track/Generate Nakatetsu Graph from JSON")]
        public static void GenerateNakatetsu()
        {
            var asset = Generate(NakatetsuManifestPath);
            Debug.Log("TrackGraph generated from JSON: " + AssetDatabase.GetAssetPath(asset), asset);
        }

        private static T Read<T>(string path) where T : class
        {
            try
            {
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
                settings.Converters.Add(new SegmentConverter());
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), settings)
                    ?? throw new InvalidDataException("JSON document is null: " + path);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid graph JSON: " + path + "\n" + exception.Message, exception);
            }
        }

        // SerializeReferenceのridやC#アセンブリ名をJSONへ持ち込まず、種類を明示する。
        private sealed class SegmentConverter : JsonConverter
        {
            public override bool CanWrite => false;

            public override bool CanConvert(Type type) => type == typeof(TrackGeometryHorizontalSegment)
                || type == typeof(TrackGeometryVerticalSegment) || type == typeof(TrackEdgeOffsetSegment);

            public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
            {
                var json = JObject.Load(reader);
                string kind = json.Value<string>("type");
                Type concrete = (type.Name, kind) switch
                {
                    (nameof(TrackGeometryHorizontalSegment), "Straight") => typeof(TrackGeometryStraightSegment),
                    (nameof(TrackGeometryHorizontalSegment), "Circular") => typeof(TrackGeometryCircularSegment),
                    (nameof(TrackGeometryHorizontalSegment), "TransitionIn") => typeof(TrackGeometryTransitionInSegment),
                    (nameof(TrackGeometryHorizontalSegment), "TransitionOut") => typeof(TrackGeometryTransitionOutSegment),
                    (nameof(TrackGeometryVerticalSegment), "ConstantGradient") => typeof(TrackGeometryConstantGradientSegment),
                    (nameof(TrackGeometryVerticalSegment), "LinearGradient") => typeof(TrackGeometryLinearGradientSegment),
                    (nameof(TrackEdgeOffsetSegment), "Constant") => typeof(TrackEdgeConstantOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "Linear") => typeof(TrackEdgeLinearOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "Cubic") => typeof(TrackEdgeCubicOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "CubicHermite") => typeof(TrackEdgeCubicHermiteOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "Arc") => typeof(TrackEdgeArcOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "TransitionIn") => typeof(TrackEdgeTransitionInOffsetSegment),
                    (nameof(TrackEdgeOffsetSegment), "TransitionOut") => typeof(TrackEdgeTransitionOutOffsetSegment),
                    _ => throw new JsonSerializationException($"Unknown {type.Name} type '{kind}'.")
                };
                return JsonUtility.FromJson(json.ToString(), concrete);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => throw new NotSupportedException();
        }
    }
}
