using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Editor
{
    [CustomEditor(typeof(TrackGraphAsset))]
    public sealed class TrackGraphAssetEditor : UnityEditor.Editor
    {
        private float integrationStepM = TrackEdgeCompiler.DefaultIntegrationStepM;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            integrationStepM = EditorGUILayout.FloatField("Integration Step (m)", integrationStepM);
            EditorGUILayout.HelpBox("Compile generates and saves all Edge distance maps using position-to-position chord lengths. Recompile after editing Geometry or offsets.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Compile Distance Maps"))
                {
                    var asset = (TrackGraphAsset)target;
                    if (!TryCompileAndSave(asset, integrationStepM, out string error)) Debug.LogError(error, asset);
                    else Debug.Log("Track distance maps compiled and saved.", asset);
                }
            }
        }

        public static bool TryCompileAndSave(TrackGraphAsset asset, float integrationStepM, out string error)
        {
            error = null;
            if (asset == null || !AssetDatabase.Contains(asset) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Select a saved TrackGraphAsset in Edit Mode.";
                return false;
            }
            if (!AssetDatabase.IsOpenForEdit(asset))
            {
                error = "TrackGraphAsset is not writable.";
                return false;
            }
            var errors = new List<string>();
            if (!TrackGraphCompiler.TryBuildDistanceMaps(asset.Definition, integrationStepM, out var maps, errors))
            {
                error = string.Join("\n", errors);
                return false;
            }
            Undo.RegisterCompleteObjectUndo(asset, "Compile Track Distance Maps");
            foreach (var edge in asset.Definition.edges) edge.distanceMap = maps[edge.edgeId];
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }
    }
}
