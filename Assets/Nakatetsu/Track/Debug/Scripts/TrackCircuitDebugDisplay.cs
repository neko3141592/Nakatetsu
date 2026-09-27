using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Circuit;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nakatetsu.Track.Debugging
{
    // 仮閉塞の範囲と、現在の占有/空きをScene・Gameで確認する。
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Debug/Track Circuit Debug Display")]
    public sealed class TrackCircuitDebugDisplay : MonoBehaviour
    {
        [SerializeField] private TrackGraphAsset graphAsset;
        [SerializeField] private TrackCircuitSimulationController circuitSimulation;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private bool showInGameView = true;
        [SerializeField, Min(0.1f)] private float sampleIntervalM = 10f;
        [SerializeField, Min(0.01f)] private float lineWidthM = 0.3f;
        [SerializeField] private float lineHeightM = 0.4f;
        [SerializeField] private Color occupiedColor = Color.red;
        [SerializeField] private Color clearColor = Color.green;
        [SerializeField] private Color editColor = Color.yellow;

        private readonly List<SectionDisplay> sections = new();
        private readonly Dictionary<string, Material> runtimeMaterials = new();
        private GameObject generatedRoot;
        private bool needsRebuild = true;

        private sealed class SectionDisplay
        {
            public readonly string CircuitId;
            public readonly Vector3[] Positions;

            public SectionDisplay(string circuitId, Vector3[] positions)
            {
                CircuitId = circuitId;
                Positions = positions;
            }
        }

        private void OnEnable() => needsRebuild = true;
        private void OnValidate() => needsRebuild = true;

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;
            EnsureBuilt();
            UpdateColors();
        }

        [ContextMenu("Rebuild Circuit Debug Display")]
        public void Rebuild()
        {
            ClearGameLines();
            sections.Clear();
            needsRebuild = false;
            if (graphAsset == null || graphAsset.Definition.circuits == null) return;

            var graph = new TrackGraphContext();
            if (!graph.TryBuildLookups(graphAsset.Definition, out string error))
            {
                Debug.LogWarning($"Track circuit debug: {error}", this);
                return;
            }

            foreach (TrackCircuitDefinition circuit in graphAsset.Definition.circuits)
            {
                if (circuit == null || circuit.sections == null) continue;
                foreach (TrackCircuitSection section in circuit.sections)
                {
                    if (TrySampleSection(graph, section, out var positions))
                        sections.Add(new SectionDisplay(circuit.circuitId, positions));
                    else
                        Debug.LogWarning($"Track circuit debug: {circuit.circuitId} / {section?.edgeId} を表示できません。", this);
                }
            }

            if (UnityEngine.Application.isPlaying && showInGameView && isActiveAndEnabled)
                CreateGameLines();
        }

        private bool TrySampleSection(TrackGraphContext graph, TrackCircuitSection section,
            out Vector3[] positions)
        {
            positions = null;
            if (section == null || !graph.TryGetEdge(section.edgeId, out var edge)
                || !edge.TryConvertToEdgeDistance(section.startDistanceOnGeometryM, out _)
                || !edge.TryConvertToEdgeDistance(section.endDistanceOnGeometryM, out _))
                return false;

            int count = Mathf.CeilToInt(Mathf.Abs(section.endDistanceOnGeometryM
                - section.startDistanceOnGeometryM) / Mathf.Max(0.1f, sampleIntervalM));
            if (count < 1 || count > 20000) return false;
            positions = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float geometryDistanceM = Mathf.Lerp(section.startDistanceOnGeometryM,
                    section.endDistanceOnGeometryM, (float)i / count);
                if (!edge.TryConvertToEdgeDistance(geometryDistanceM, out float edgeDistanceM)
                    || !TrackEdgeCalculator.TryEvaluate(graph, edge.edgeId, edgeDistanceM, out var sample))
                    return false;
                positions[i] = sample.Position + Vector3.up * lineHeightM;
            }
            return true;
        }

        private void EnsureBuilt()
        {
            if (needsRebuild) Rebuild();
        }

        private void CreateGameLines()
        {
            if (sections.Count == 0) return;
            Shader shader = lineMaterial == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (lineMaterial == null && shader == null) shader = Shader.Find("Sprites/Default");
            if (lineMaterial == null && shader == null)
            {
                Debug.LogWarning("Track circuit debug: Line Materialを指定してください。", this);
                return;
            }

            generatedRoot = new GameObject("Track Circuit Debug Lines") { hideFlags = HideFlags.HideAndDontSave };
            generatedRoot.transform.SetParent(transform, false);
            foreach (SectionDisplay section in sections)
            {
                if (!runtimeMaterials.TryGetValue(section.CircuitId, out var material))
                {
                    material = lineMaterial != null ? new Material(lineMaterial) : new Material(shader);
                    material.hideFlags = HideFlags.HideAndDontSave;
                    runtimeMaterials.Add(section.CircuitId, material);
                }

                var lineObject = new GameObject(section.CircuitId) { hideFlags = HideFlags.HideAndDontSave };
                lineObject.layer = gameObject.layer;
                lineObject.transform.SetParent(generatedRoot.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.widthMultiplier = Mathf.Max(0.01f, lineWidthM);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.positionCount = section.Positions.Length;
                line.SetPositions(section.Positions);
            }
            UpdateColors();
        }

        private Color ColorFor(string circuitId) => circuitSimulation == null || circuitSimulation.IsOccupied(circuitId)
            ? occupiedColor : clearColor;

        private void UpdateColors()
        {
            foreach (var pair in runtimeMaterials)
            {
                Color color = ColorFor(pair.Key);
                if (pair.Value.HasProperty("_BaseColor")) pair.Value.SetColor("_BaseColor", color);
                if (pair.Value.HasProperty("_Color")) pair.Value.SetColor("_Color", color);
            }
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying || !showInGameView || graphAsset == null
                || graphAsset.Definition.circuits == null || Event.current.type != EventType.Repaint) return;

            var circuits = graphAsset.Definition.circuits;
            float x = Screen.width - 208f;
            GUI.Box(new Rect(x, 12f, 196f, 30f + circuits.Count * 20f), "Track circuits");
            Color oldColor = GUI.contentColor;
            for (int i = 0; i < circuits.Count; i++)
            {
                var circuit = circuits[i];
                if (circuit == null) continue;
                bool occupied = circuitSimulation == null || circuitSimulation.IsOccupied(circuit.circuitId);
                GUI.contentColor = occupied ? occupiedColor : clearColor;
                GUI.Label(new Rect(x + 10f, 38f + i * 20f, 176f, 20f),
                    $"{circuit.circuitId}  {(occupied ? "OCCUPIED" : "CLEAR")}");
            }
            GUI.contentColor = oldColor;
        }

        private void OnDrawGizmos()
        {
            if (!isActiveAndEnabled || !showInSceneView || Camera.current == null
                || Camera.current.cameraType != CameraType.SceneView) return;
            EnsureBuilt();
            Color oldColor = Gizmos.color;
            var labeled = new HashSet<string>();
            foreach (SectionDisplay section in sections)
            {
                Gizmos.color = UnityEngine.Application.isPlaying ? ColorFor(section.CircuitId) : editColor;
                for (int i = 1; i < section.Positions.Length; i++)
                    Gizmos.DrawLine(section.Positions[i - 1], section.Positions[i]);
#if UNITY_EDITOR
                if (labeled.Add(section.CircuitId))
                    Handles.Label(section.Positions[section.Positions.Length / 2] + Vector3.up,
                        section.CircuitId);
#endif
            }
            Gizmos.color = oldColor;
        }

        private void OnDisable()
        {
            ClearGameLines();
            needsRebuild = true;
        }

        private void OnDestroy() => ClearGameLines();

        private void ClearGameLines()
        {
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
                DestroyGenerated(generatedRoot);
                generatedRoot = null;
            }
            foreach (Material material in runtimeMaterials.Values) DestroyGenerated(material);
            runtimeMaterials.Clear();
        }

        private static void DestroyGenerated(Object value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
