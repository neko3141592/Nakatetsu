using System;
using System.Collections.Generic;
using System.Linq;
using Nakatetsu.Application.Audio;
using Nakatetsu.Application.Debugging;
using Nakatetsu.Application.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nakatetsu.Application.Player.Editor
{
    public static class ApplicationSceneSetup
    {
        private const string UndoName = "Organize Application hierarchy";

        [MenuItem("Nakatetsu/Setup/Organize NtLine Application")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/Scenes/NtLine.unity" || scene.isDirty)
            {
                Debug.LogWarning("NtLineを保存し、Playモードを終了してから実行してください。");
                return;
            }

            GameObject application = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "Application");
            if (application == null)
            {
                Debug.LogError("シーン直下のApplicationが見つかりません。");
                return;
            }

            if (TryOrganize(application) && EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("ApplicationをSimulation・Focus・Input・Audio・Main Cameraに整理しました。");
            }
        }

        public static bool TryOrganize(GameObject application)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            try
            {
                // 複数ある場合はSingleOrDefaultで止め、設定を勝手に選ばない。
                var simulation = application.GetComponentsInChildren<ApplicationSimulationController>(true).SingleOrDefault();
                var focus = application.GetComponentsInChildren<TrainFocusController>(true).SingleOrDefault();
                var audio = application.GetComponentsInChildren<ApplicationAudioController>(true).SingleOrDefault();
                var input = application.GetComponentsInChildren<PlayerInput>(true).SingleOrDefault();
                var receiver = application.GetComponentsInChildren<TrainKeyboardInputController>(true).SingleOrDefault();
                var debugPanel = application.GetComponentsInChildren<ApplicationSimulationDebugPanel>(true).SingleOrDefault();
                Camera camera = application.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .SingleOrDefault(candidate => candidate.CompareTag("MainCamera"));

                if (simulation == null || camera == null)
                {
                    throw new InvalidOperationException("ApplicationのSimulationとMainCameraタグのカメラを確認してください。");
                }

                GameObject simulationObject = GetOrCreateChild(application, "Simulation");
                GameObject focusObject = GetOrCreateChild(application, "Focus");
                GameObject inputObject = GetOrCreateChild(application, "Input");
                GameObject audioObject = GetOrCreateChild(application, "Audio");
                var replacements = new Dictionary<Component, Component>();
                var originals = new List<Component>();

                CopyTo(simulation, simulationObject, replacements, originals);
                focus = CopyTo(focus, focusObject, replacements, originals);
                audio = CopyTo(audio, audioObject, replacements, originals);
                input = CopyTo(input, inputObject, replacements, originals);
                receiver = CopyTo(receiver, inputObject, replacements, originals);
                if (debugPanel != null)
                {
                    CopyTo(debugPanel, simulationObject, replacements, originals);
                }

                // UnityEventsを含むシーン内の参照を新しいコンポーネントへ引き継ぐ。
                ReplaceSceneReferences(application.scene, replacements);
                SetReference(focus, "keyboardInput", receiver);
                SetReference(audio, "focusController", focus);
                if (focus.FocusedTrain != null)
                {
                    SetReference(receiver, "target", focus.FocusedTrain);
                }

                var cameraController = camera.GetComponent<TrainCameraController>();
                if (cameraController == null)
                {
                    cameraController = Undo.AddComponent<TrainCameraController>(camera.gameObject);
                }

                SetReference(cameraController, "focusController", focus);
                if (camera.transform.parent != application.transform)
                {
                    Undo.SetTransformParent(camera.transform, application.transform, true, UndoName);
                }

                // RequireComponentの依存先より先に、依存する側を削除する。
                for (int i = originals.Count - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(originals[i]);
                }

                EditorSceneManager.MarkSceneDirty(application.scene);
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError($"Applicationの整理を中止しました。{exception.Message}", application);
                return false;
            }
        }

        private static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var result = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(result, UndoName);
            Undo.SetTransformParent(result.transform, parent.transform, false, UndoName);
            return result;
        }

        private static T CopyTo<T>(T source, GameObject destination,
            Dictionary<Component, Component> replacements, List<Component> originals) where T : Component
        {
            if (source != null && source.gameObject == destination)
            {
                return source;
            }

            T result = Undo.AddComponent<T>(destination);
            if (source != null)
            {
                Undo.RecordObject(result, UndoName);
                EditorUtility.CopySerialized(source, result);
                replacements.Add(source, result);
                originals.Add(source);
            }

            return result;
        }

        private static void ReplaceSceneReferences(Scene scene, Dictionary<Component, Component> replacements)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue is Component oldReference &&
                            replacements.TryGetValue(oldReference, out Component newReference))
                        {
                            property.objectReferenceValue = newReference;
                        }
                    }

                    serialized.ApplyModifiedProperties();
                }
            }
        }

        internal static void SetReference(Component component, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
