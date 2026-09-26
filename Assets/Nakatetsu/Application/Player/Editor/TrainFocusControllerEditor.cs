using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Application.Player.Editor
{
    [CustomEditor(typeof(TrainFocusController))]
    public sealed class TrainFocusControllerEditor : UnityEditor.Editor
    {
        [SerializeField] private string debugTrainId = "1001F";

        private string lastResult;
        private MessageType lastResultType;

        public override bool RequiresConstantRepaint()
        {
            return EditorApplication.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("trains"), true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                // 実行中は切り替え処理を通し、キー入力先も一緒に変更する。
                EditorGUILayout.PropertyField(serializedObject.FindProperty("focusedTrain"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("keyboardInput"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("注目編成のデバッグ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Playモードで、注目したい編成のTrainRootに設定したTrain Idを指定してください。", MessageType.Info);
            debugTrainId = EditorGUILayout.TextField("Train Id", debugTrainId);

            var controller = (TrainFocusController)target;
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying ||
                !controller.isActiveAndEnabled || string.IsNullOrWhiteSpace(debugTrainId)))
            {
                if (GUILayout.Button("この編成に注目"))
                {
                    string trainId = debugTrainId.Trim();
                    if (controller.TryFocusTrainByTrainId(trainId))
                    {
                        lastResult = $"{trainId}に注目を切り替えました。";
                        lastResultType = MessageType.Info;
                    }
                    else
                    {
                        lastResult = "注目を切り替えられませんでした。Trains内の編成のTrain Idと、Keyboard Inputの設定を確認してください。";
                        lastResultType = MessageType.Warning;
                    }
                }
            }

            if (EditorApplication.isPlaying && !string.IsNullOrEmpty(lastResult))
            {
                EditorGUILayout.HelpBox(lastResult, lastResultType);
            }
        }
    }
}
