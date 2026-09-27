using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nakatetsu.Application.Player.Editor
{
    public static class TrainHudSceneSetup
    {
        [MenuItem("Nakatetsu/Setup/Connect NtLine HUD")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/Scenes/NtLine.unity" || scene.isDirty)
            {
                Debug.LogWarning("NtLineを保存し、Playモードを終了してから実行してください。");
                return;
            }

            GameObject application = GameObject.Find("Application");
            Transform hudTransform = application == null
                ? null
                : application.transform.Find("HUD/Canvas/TrainHUD");
            RectTransform hud = hudTransform as RectTransform;
            TrainFocusController focus = application == null
                ? null
                : application.GetComponentInChildren<TrainFocusController>(true);
            TMP_Text reverser = FindText(hud, "Reverser");
            TMP_Text notch = FindText(hud, "Notch");
            if (hud == null || focus == null || reverser == null || notch == null)
            {
                Debug.LogError("Application/FocusとHUD/Canvas/TrainHUDのReverser・Notchを確認してください。");
                return;
            }

            // 既存の操作表示の下に、未作成の項目だけを追加する。
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(hud, reverser.rectTransform);
            bounds.Encapsulate(RectTransformUtility.CalculateRelativeRectTransformBounds(hud, notch.rectTransform));
            Vector2 topLeft = new Vector2(bounds.min.x - hud.rect.xMin, bounds.min.y - hud.rect.yMax - 12f);
            TMP_Text speed = FindText(hud, "Speed");
            if (speed == null)
            {
                speed = CreateText(hud, "Speed", notch, topLeft, new Vector2(320f, 60f), 48f);
            }

            TMP_Text trainInfo = FindText(hud, "TrainInfo");
            if (trainInfo == null)
            {
                trainInfo = CreateText(hud, "TrainInfo", reverser,
                    topLeft + new Vector2(0f, -68f), new Vector2(380f, 32f), 20f);
            }

            TrainHudController controller = hud.GetComponent<TrainHudController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<TrainHudController>(hud.gameObject);
            }

            Undo.RecordObjects(new Object[] { controller, reverser, notch, speed, trainInfo }, "Connect train HUD");
            ApplicationSceneSetup.SetReference(controller, "focusController", focus);
            ApplicationSceneSetup.SetReference(controller, "speedText", speed);
            ApplicationSceneSetup.SetReference(controller, "notchText", notch);
            ApplicationSceneSetup.SetReference(controller, "reverserText", reverser);
            ApplicationSceneSetup.SetReference(controller, "trainInfoText", trainInfo);

            // 編集時は実行時の状態がないため、未取得の表示にする。
            speed.text = "0 km/h";
            notch.text = "";
            reverser.text = "";
            trainInfo.text = "";
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("NtLine HUD connected: speed, notch, reverser, train ID and active cab.");
            }
        }

        private static TMP_Text FindText(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(name);
            if (child == null)
            {
                return null;
            }

            return child.GetComponent<TMP_Text>();
        }

        private static TMP_Text CreateText(RectTransform parent, string name, TMP_Text style,
            Vector2 position, Vector2 size, float fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, "Create HUD text");
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);

            var text = Undo.AddComponent<TextMeshProUGUI>(textObject);
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontStyle = style.fontStyle;
            text.color = style.color;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }
    }
}
