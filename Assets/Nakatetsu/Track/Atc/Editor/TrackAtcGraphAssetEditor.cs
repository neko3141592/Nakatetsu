using System.Collections.Generic;
using Nakatetsu.Track.Interlocking;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Atc.Editor
{
    [CustomEditor(typeof(TrackAtcGraphAsset))]
    public sealed class TrackAtcGraphAssetEditor : UnityEditor.Editor
    {
        private string message;
        private MessageType messageType;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "線形を変更した場合は、先にTrack GraphのCompile Distance Mapsを実行してください。" +
                "コンパイル成功時に、このAssetへATC Graphを保存します。",
                MessageType.Info);

            var asset = (TrackAtcGraphAsset)target;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Compile ATC Graph"))
                {
                    if (TryCompileAndSave(asset, out string error))
                    {
                        var graph = asset.Definition;
                        message = $"保存しました: {graph.atcEdge.Count} Edges / " +
                            $"{graph.atcNode.Count} Nodes / {graph.routes.Count} Routes";
                        messageType = MessageType.Info;
                    }
                    else
                    {
                        message = error;
                        messageType = MessageType.Error;
                        Debug.LogError(error, asset);
                    }
                }
            }

            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, messageType);

            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("definition"),
                    new GUIContent("Compiled Graph"), true);
        }

        public static bool TryCompileAndSave(TrackAtcGraphAsset asset, out string error)
        {
            error = null;
            if (asset == null || !AssetDatabase.Contains(asset) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "保存済みのTrackAtcGraphAssetをEdit Modeで選択してください。";
                return false;
            }
            if (!AssetDatabase.IsOpenForEdit(asset))
            {
                error = "TrackAtcGraphAssetへ書き込めません。";
                return false;
            }
            if (asset.TrackGraph == null || asset.Source == null || asset.Interlockings == null)
            {
                error = "Track GraphとSourceを指定してください。Interlockingsは空の一覧でも構いません。";
                return false;
            }

            var definitions = new List<TrackInterlockingDefinition>();
            for (int i = 0; i < asset.Interlockings.Count; i++)
            {
                var interlocking = asset.Interlockings[i];
                if (interlocking == null)
                {
                    error = $"Interlockings[{i}]が未指定です。Assetを指定するか項目を削除してください。";
                    return false;
                }
                definitions.Add(interlocking.Definition);
            }

            var errors = new List<string>();
            if (!TrackAtcGraphCompiler.TryCompile(asset.TrackGraph.Definition, asset.Source.Definition,
                definitions, out var compiled, errors))
            {
                error = string.Join("\n", errors);
                return false;
            }

            Undo.RegisterCompleteObjectUndo(asset, "Compile ATC Graph");
            asset.SetCompiledDefinition(compiled);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }
    }
}
