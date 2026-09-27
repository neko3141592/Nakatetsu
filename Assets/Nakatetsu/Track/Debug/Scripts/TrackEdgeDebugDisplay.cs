using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nakatetsu.Track.Debugging
{
    /// <summary>Graphのワールド座標をScene/Gameに表示する開発用オーバーレイ。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Debug/Track Edge Debug Display")]
    public sealed class TrackEdgeDebugDisplay : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private TrackGraphAsset graphAsset;
        [Tooltip("未指定ならMainCameraを使用。TIMSなどの描画用カメラには表示しない。")]
        [SerializeField] private Camera gameCamera;

        [Header("Visibility")]
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private bool showInGameView = true;
        [Tooltip("カメラからのラベル表示距離。0なら距離制限なし。線には適用しない。")]
        [SerializeField, Min(0f)] private float labelVisibleDistanceM = 500f;

        [Header("Edge Lines")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = Color.cyan;
        [SerializeField, Min(0.01f)] private float lineWidthM = 0.12f;
        [SerializeField, Min(0.1f)] private float lineSampleIntervalM = 5f;
        [Tooltip("線が地面に埋まらないようにワールドY方向へ持ち上げる量。")]
        [SerializeField] private float lineHeightM = 0.15f;

        [Header("Distance Labels (Every 50 m)")]
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField, Range(10, 32)] private int labelFontSize = 14;
        [SerializeField] private Vector3 labelWorldOffset = new(0f, 1.5f, 0f);

        private readonly List<TrackEdgeDebugSamples> sampledEdges = new();
        private bool needsRebuild = true;
        private GameObject generatedRoot;
        private Material runtimeMaterial;
        private GUIStyle gameLabelStyle;
#if UNITY_EDITOR
        private GUIStyle sceneLabelStyle;
#endif

        public IReadOnlyList<TrackEdgeDebugSamples> SampledEdges => sampledEdges;

        private void OnEnable() => needsRebuild = true;

        private void OnValidate()
        {
            needsRebuild = true;
            gameLabelStyle = null;
#if UNITY_EDITOR
            sceneLabelStyle = null;
#endif
        }

        private void Update() => EnsureSamples();

        [ContextMenu("Rebuild Edge Debug Display")]
        public void Rebuild()
        {
            needsRebuild = false;
            ClearGameLines();
            sampledEdges.Clear();
            if (graphAsset == null) return;

            var graph = new TrackGraphContext();
            if (!graph.TryBuildLookups(graphAsset.Definition, out string error))
            {
                Debug.LogWarning($"Track edge debug: {error}", this);
                return;
            }

            foreach (var edge in graphAsset.Definition.edges)
            {
                if (TrackEdgeDebugSampling.TrySample(graph, edge, lineSampleIntervalM, out var samples))
                    sampledEdges.Add(samples);
                else
                    Debug.LogWarning($"Track edge debug: {edge?.edgeId} の距離表・線形が無効なため表示を省略しました。", this);
            }

            if (UnityEngine.Application.IsPlaying(gameObject) && showInGameView && isActiveAndEnabled)
                CreateGameLines();
        }

        private void EnsureSamples()
        {
            if (needsRebuild) Rebuild();
        }

        private void CreateGameLines()
        {
            if (sampledEdges.Count == 0) return;
            if (lineMaterial != null)
                runtimeMaterial = new Material(lineMaterial);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogWarning("Track edge debug: Line Materialを指定してください。", this);
                    return;
                }
                runtimeMaterial = new Material(shader);
            }
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", lineColor);
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", lineColor);

            generatedRoot = new GameObject("Track Edge Debug Lines") { hideFlags = HideFlags.HideAndDontSave };
            generatedRoot.transform.SetParent(transform, false);
            foreach (var edge in sampledEdges)
            {
                var lineObject = new GameObject(edge.EdgeId) { hideFlags = HideFlags.HideAndDontSave };
                lineObject.layer = gameObject.layer;
                lineObject.transform.SetParent(generatedRoot.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = runtimeMaterial;
                line.useWorldSpace = true;
                line.widthMultiplier = Mathf.Max(0.01f, lineWidthM);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.positionCount = edge.LinePositions.Length;
                for (int i = 0; i < edge.LinePositions.Length; i++)
                    line.SetPosition(i, edge.LinePositions[i] + Vector3.up * lineHeightM);
            }
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.IsPlaying(gameObject) || !showInGameView || Event.current.type != EventType.Repaint)
                return;
            var camera = gameCamera != null ? gameCamera : Camera.main;
            if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture != null) return;
            EnsureSamples();
            if (gameLabelStyle == null)
            {
                gameLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = labelFontSize, alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(5, 5, 2, 2)
                };
                gameLabelStyle.normal.textColor = labelColor;
            }
            var oldColor = GUI.color;
            foreach (var edge in sampledEdges)
            foreach (var label in edge.Labels)
            {
                Vector3 position = label.Position + labelWorldOffset;
                if (!IsLabelVisible(camera, position)) continue;
                Vector3 screen = camera.WorldToScreenPoint(position);
                if (screen.z < camera.nearClipPlane || screen.z > camera.farClipPlane
                    || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) continue;
                var content = new GUIContent(label.Text);
                Vector2 size = gameLabelStyle.CalcSize(content);
                var rect = new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y / 2f, size.x, size.y);
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(rect, content, gameLabelStyle);
            }
            GUI.color = oldColor;
        }

        private bool IsLabelVisible(Camera camera, Vector3 position) => labelVisibleDistanceM <= 0f
            || (camera.transform.position - position).sqrMagnitude <= labelVisibleDistanceM * labelVisibleDistanceM;

        private void OnDrawGizmos()
        {
            if (!isActiveAndEnabled || !showInSceneView
                || Camera.current == null || Camera.current.cameraType != CameraType.SceneView) return;
            EnsureSamples();
            Color previousColor = Gizmos.color;
            Gizmos.color = lineColor;
            foreach (var edge in sampledEdges)
            {
                for (int i = 1; i < edge.LinePositions.Length; i++)
                    Gizmos.DrawLine(edge.LinePositions[i - 1] + Vector3.up * lineHeightM,
                        edge.LinePositions[i] + Vector3.up * lineHeightM);
#if UNITY_EDITOR
                var camera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null;
                if (camera == null) continue;
                if (sceneLabelStyle == null)
                {
                    sceneLabelStyle = new GUIStyle(EditorStyles.helpBox)
                    { fontSize = labelFontSize, alignment = TextAnchor.MiddleCenter };
                    sceneLabelStyle.normal.textColor = labelColor;
                }
                foreach (var label in edge.Labels)
                {
                    Vector3 position = label.Position + labelWorldOffset;
                    if (IsLabelVisible(camera, position)) Handles.Label(position, label.Text, sceneLabelStyle);
                }
#endif
            }
            Gizmos.color = previousColor;
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
            if (runtimeMaterial != null)
            {
                DestroyGenerated(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        private static void DestroyGenerated(Object value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
