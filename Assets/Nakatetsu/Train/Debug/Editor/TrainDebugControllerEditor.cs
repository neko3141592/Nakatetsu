using Nakatetsu.Train.Debugging;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Debugging.Editor
{
    [CustomEditor(typeof(TrainDebugController))]
    public sealed class TrainDebugControllerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (TrainDebugController)target;
            EditorGUILayout.HelpBox("TrainRootのある編成ルートに追加し、Play後に運転台を準備 → Pノッチで発進します。車両モデルの線路上移動は別途接続が必要です。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("機器を再取得")) controller.RefreshReferences();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("前側運転台を準備")) controller.PrepareFrontCab();
                if (GUILayout.Button("後側運転台を準備")) controller.PrepareRearCab();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("レバーサー（選択中の運転台基準・停車かつN）");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("後進")) controller.SetReverser(-1);
                if (GUILayout.Button("中立")) controller.SetReverser(0);
                if (GUILayout.Button("前進")) controller.SetReverser(1);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("N（惰行／停車後の非常解除）")) controller.SetNeutral();
                EditorGUILayout.BeginHorizontal();
                for (int i = 1; i <= controller.MaxPowerPosition; i++)
                    if (GUILayout.Button($"P{i}")) controller.SetPower(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                for (int i = 1; i <= controller.MaxBrakePosition; i++)
                    if (GUILayout.Button($"B{i}")) controller.SetBrake(i);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("非常ブレーキ")) controller.SetEmergencyBrake();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("ドア（全車・編成前方を基準とした左右）");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("左側を開く")) controller.OpenLeftDoors();
                if (GUILayout.Button("右側を開く")) controller.OpenRightDoors();
                if (GUILayout.Button("両側を閉じる")) controller.CloseDoors();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox(controller.LastOperation, MessageType.None);
            if (Application.isPlaying) EditorGUILayout.HelpBox(controller.GetStatus(), MessageType.None);
        }
    }
}
