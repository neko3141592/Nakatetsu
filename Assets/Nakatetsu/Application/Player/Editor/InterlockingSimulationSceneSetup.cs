using System;
using System.Collections.Generic;
using System.Linq;
using Nakatetsu.Application.Simulation;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nakatetsu.Application.Player.Editor
{
    public static class InterlockingSimulationSceneSetup
    {
        private const string UndoName = "Connect interlocking management";

        [MenuItem("Nakatetsu/Setup/Connect NtLine Interlocking Management")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/NtLine.unity")
            {
                Debug.LogWarning("NtLineを開き、Playモードを終了してから実行してください。");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                var roots = scene.GetRootGameObjects();
                var simulation = FindSingle<ApplicationSimulationController>(roots);
                var atc = FindSingle<TrackAtcController>(roots);
                var management = roots.SelectMany(root =>
                    root.GetComponentsInChildren<TrackInterlockingManagementController>(true)).SingleOrDefault();
                if (management == null)
                {
                    var managementObject = new GameObject("InterlockingManagement");
                    Undo.RegisterCreatedObjectUndo(managementObject, UndoName);
                    SceneManager.MoveGameObjectToScene(managementObject, scene);
                    management = Undo.AddComponent<TrackInterlockingManagementController>(managementObject);
                }

                var serializedManagement = new SerializedObject(management);
                var registrations = serializedManagement.FindProperty("stations");
                if (registrations.arraySize == 0)
                {
                    var stations = roots.SelectMany(root =>
                        root.GetComponentsInChildren<TrackStationInterlockingController>(true)).ToArray();
                    if (stations.Length == 0)
                    {
                        throw new InvalidOperationException("登録する駅連動装置が見つかりません。");
                    }
                    var stationIds = new HashSet<string>();
                    registrations.arraySize = stations.Length;
                    for (int i = 0; i < stations.Length; i++)
                    {
                        var serializedStation = new SerializedObject(stations[i]);
                        var asset = serializedStation.FindProperty("interlockingAsset").objectReferenceValue
                            as TrackStationInterlockingAsset;
                        string stationId = asset != null ? asset.Definition.interlockingId : null;
                        if (string.IsNullOrWhiteSpace(stationId) || !stationIds.Add(stationId))
                        {
                            throw new InvalidOperationException("駅連動定義のIDの未設定または重複を修正してください。");
                        }
                        var registration = registrations.GetArrayElementAtIndex(i);
                        registration.FindPropertyRelative("stationId").stringValue = stationId;
                        registration.FindPropertyRelative("interlocking").objectReferenceValue = stations[i];
                    }
                    serializedManagement.ApplyModifiedProperties();
                }

                SetManagementReference(simulation, management);
                SetManagementReference(atc, management);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("連動管理部をApplicationとATCに接続しました。Sceneは未保存です。", management);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError($"連動管理部の接続を中止しました。{exception.Message}");
            }
        }

        private static T FindSingle<T>(GameObject[] roots) where T : Component
        {
            var component = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).SingleOrDefault();
            if (component == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name}を1つ配置してください。");
            }
            return component;
        }

        private static void SetManagementReference(Component target, TrackInterlockingManagementController management)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty("interlockingManagement").objectReferenceValue = management;
            serialized.ApplyModifiedProperties();
        }
    }
}
