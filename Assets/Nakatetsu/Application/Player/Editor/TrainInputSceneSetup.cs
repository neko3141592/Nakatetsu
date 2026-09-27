using System.Linq;
using Nakatetsu.Train;
using Nakatetsu.Train.Integration;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nakatetsu.Application.Player.Editor
{
    public static class TrainInputSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/NtLine.unity";
        private const string ActionsPath = "Assets/Nakatetsu/Application/Player/Data/TrainInputActions.inputactions";

        [MenuItem("Nakatetsu/Setup/Connect NtLine Keyboard Input")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            {
                Debug.LogWarning("NtLineを保存した状態で、Playモードを終了してから実行してください。未保存のシーンは変更しません。");
                return;
            }

            var roots = scene.GetRootGameObjects();
            TrainRoot train = roots.SelectMany(root => root.GetComponentsInChildren<TrainRoot>(true))
                .SingleOrDefault(root => root.name == "1001F");
            GameObject application = roots.SingleOrDefault(root => root.name == "Application");
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (train == null || application == null || actions == null)
            {
                Debug.LogError("1001F・Application・TrainInputActionsの配置を確認してください。");
                return;
            }

            if (!ApplicationSceneSetup.TryOrganize(application))
            {
                return;
            }

            TrainIntegrationController integration = FindOrCreateIntegration(train);
            if (integration == null)
            {
                return;
            }
            GameObject inputObject = application.transform.Find("Input").gameObject;
            var input = GetOrAdd<PlayerInput>(inputObject);
            var receiver = GetOrAdd<TrainKeyboardInputController>(inputObject);
            var focus = application.transform.Find("Focus").GetComponent<TrainFocusController>();
            Undo.RecordObjects(new Object[] { input, receiver }, "Connect train keyboard input");
            if (focus.FocusedTrain == null)
            {
                ApplicationSceneSetup.SetReference(focus, "focusedTrain", integration);
            }

            receiver.SetTarget(focus.FocusedTrain);
            input.actions = actions;
            input.defaultActionMap = "Driving";
            input.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            input.actionEvents = new[]
            {
                Bind(actions, "NotchTowardBrake", receiver.OnNotchTowardBrake),
                Bind(actions, "NotchTowardPower", receiver.OnNotchTowardPower),
                Bind(actions, "NotchNeutral", receiver.OnNotchNeutral),
                Bind(actions, "ReverserForward", receiver.OnReverserForward),
                Bind(actions, "ReverserBackward", receiver.OnReverserBackward),
                Bind(actions, "EbReset", receiver.OnEbReset)
            };
            EditorUtility.SetDirty(input);
            EditorUtility.SetDirty(receiver);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("NtLine keyboard input connected through TrainIntegrationController.");
            }
        }

        private static TrainIntegrationController FindOrCreateIntegration(TrainRoot train)
        {
            TrainIntegrationController existing = null;
            foreach (TrainIntegrationController candidate in train.GetComponentsInChildren<TrainIntegrationController>(true))
            {
                if (candidate.GetComponentInParent<TrainRoot>(true) != train)
                {
                    continue;
                }

                if (existing != null)
                {
                    Debug.LogError("編成内のTrainIntegrationControllerは1つにしてください。", train);
                    return null;
                }

                existing = candidate;
            }

            // 配置済みなら再利用し、入力先を重複作成しない。
            if (existing != null)
            {
                return existing;
            }

            Transform integrationRoot = train.transform.Find("Integration");
            if (integrationRoot == null)
            {
                var integrationObject = new GameObject("Integration");
                Undo.RegisterCreatedObjectUndo(integrationObject, "Create train integration");
                Undo.SetTransformParent(integrationObject.transform, train.transform, "Parent train integration");
                integrationRoot = integrationObject.transform;
                integrationRoot.localPosition = Vector3.zero;
                integrationRoot.localRotation = Quaternion.identity;
                integrationRoot.localScale = Vector3.one;
            }

            return GetOrAdd<TrainIntegrationController>(integrationRoot.gameObject);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent<T>(out var component))
            {
                return component;
            }

            return Undo.AddComponent<T>(target);
        }

        private static PlayerInput.ActionEvent Bind(InputActionAsset actions, string name,
            UnityAction<InputAction.CallbackContext> callback)
        {
            var result = new PlayerInput.ActionEvent(actions.FindAction("Driving/" + name, true));
            UnityEventTools.AddPersistentListener(result, callback);
            return result;
        }
    }
}
