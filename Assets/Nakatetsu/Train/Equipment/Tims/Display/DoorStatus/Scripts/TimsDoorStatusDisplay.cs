using System.Collections.Generic;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.DoorStatus
{
    /// <summary>編成順に各車のドア全閉接点を表示する。文字は全車「開」で固定。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Nakatetsu/Tims/Door Status Display")]
    public sealed class TimsDoorStatusDisplay : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("未指定なら親TIMS、または同じTrainRoot内のTIMSを使用します。編成定義はTrainRootから取得します。")]
        [SerializeField] private TimsCommunicationController tims;
        [Tooltip("編集用の見本。実行時は非表示にします。")]
        [SerializeField] private GameObject editorSample;

        [Header("Binding")]
        [Tooltip("Localは各号車のBus、Masterは全車共通のBusを読みます。")]
        [SerializeField] private TimsBusTarget busTarget = TimsBusTarget.Local;
        [SerializeField] private string deviceName = "Door";
        [SerializeField] private string itemName = "AllClosed";
        [Tooltip("オン: trueを全閉として表示。オフ: trueを開として表示。参照タグはboolです。")]
        [SerializeField] private bool valueMeansClosed = true;

        [Header("Layout")]
        [Tooltip("編成全体の中心位置。TrainStatusと同じく間隔×(両数−1)/2だけ左から生成します。")]
        [SerializeField] private Vector2 centerPosition;
        [SerializeField, Min(0f)] private float spacingX = 80f;
        [Tooltip("素材内の透明余白を含むRectサイズ。")]
        [SerializeField] private Vector2 spriteSize = new(1024f, 1024f);

        [Header("Sprites")]
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite closedSprite;

        [Header("Label")]
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField, Min(1f)] private float labelFontSize = 24f;
        [SerializeField] private Vector2 labelOffset;
        [SerializeField] private Vector2 labelSize = new(76f, 36f);
        [SerializeField] private Color openLabelColor = Color.black;
        [SerializeField] private Color closedLabelColor = new(.15f, .16f, .18f, 1f);
        [Tooltip("欠測時はスプライトを隠し、この色の「開」を表示します。")]
        [SerializeField] private Color unavailableLabelColor = Color.gray;

        [SerializeField, HideInInspector] private RectTransform generatedRoot;
        private readonly List<CarVisual> cars = new();

        private sealed class CarVisual
        {
            public Image image;
            public TextMeshProUGUI label;
        }

        public int GeneratedCarCount => cars.Count;
        public RectTransform GeneratedRoot => generatedRoot;

        private void OnEnable()
        {
            if (Application.isPlaying && editorSample != null) editorSample.SetActive(false);
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void OnDisable()
        {
            if (generatedRoot != null) generatedRoot.gameObject.SetActive(false);
        }
        private void OnDestroy() => ClearGenerated();

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            Refresh();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            TrainRoot owner = ResolveOwner();
            ConsistDefinitionAsset definition = owner != null ? owner.ConsistDefinition : null;
            int count = definition != null ? definition.CarCount : 0;
            if (count == 0) { ClearGenerated(); return; }
            if (NeedsRebuild(count)) Build(count);
            if (editorSample != null) editorSample.SetActive(false);
            generatedRoot.gameObject.SetActive(true);
            bool sourceAvailable = tims != null && tims.isActiveAndEnabled && owner.isActiveAndEnabled;
            Vector2 firstPosition = centerPosition - Vector2.right * (spacingX * (count - 1) * .5f);
            for (int i = 0; i < count; i++)
            {
                CarVisual visual = cars[i];
                bool hasCar = definition.cars[i] != null;
                visual.image.gameObject.SetActive(hasCar);
                visual.label.gameObject.SetActive(hasCar);
                if (!hasCar) continue;
                bool closed = false;
                bool hasState = sourceAvailable && TryReadClosed(i, out closed);
                visual.image.sprite = hasState ? (closed ? closedSprite : openSprite) : null;
                visual.image.enabled = visual.image.sprite != null;
                visual.label.color = !hasState ? unavailableLabelColor : closed ? closedLabelColor : openLabelColor;
                Vector2 position = firstPosition + Vector2.right * (spacingX * i);
                visual.image.rectTransform.anchoredPosition = position;
                visual.image.rectTransform.sizeDelta = spriteSize;
                visual.label.rectTransform.anchoredPosition = position + labelOffset;
                visual.label.rectTransform.sizeDelta = labelSize;
                TMP_FontAsset font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
                if (visual.label.font != font) visual.label.font = font;
                visual.label.fontSize = labelFontSize;
            }
        }

        private bool TryReadClosed(int carIndex, out bool closed)
        {
            closed = false;
            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(itemName)) return false;
            TimsBusState bus = null;
            if (busTarget == TimsBusTarget.Master) bus = tims.MasterBus;
            else if (busTarget == TimsBusTarget.Local) tims.TryGetLocalBus(carIndex, out bus);
            if (bus == null || !bus.TryGetBool(new TimsTagKey(deviceName, itemName), out bool value)) return false;
            closed = valueMeansClosed ? value : !value;
            return true;
        }

        private TrainRoot ResolveOwner()
        {
            if (tims == null)
            {
                tims = GetComponentInParent<TimsCommunicationController>(true);
                if (tims == null)
                {
                    var owner = GetComponentInParent<TrainRoot>(true);
                    if (owner != null) tims = owner.GetComponentInChildren<TimsCommunicationController>(true);
                }
            }
            return tims != null ? tims.GetComponentInParent<TrainRoot>(true) : GetComponentInParent<TrainRoot>(true);
        }

        private bool NeedsRebuild(int count)
        {
            if (generatedRoot == null || cars.Count != count) return true;
            foreach (CarVisual car in cars)
                if (car.image == null || car.label == null) return true;
            return false;
        }

        private void Build(int count)
        {
            ClearGenerated();
            generatedRoot = CreateRect("GeneratedDoors", transform);
            generatedRoot.anchorMin = Vector2.zero;
            generatedRoot.anchorMax = Vector2.one;
            generatedRoot.sizeDelta = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var image = CreateRect($"Door_{i + 1:00}", generatedRoot).gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                cars.Add(new CarVisual { image = image });
            }
            for (int i = 0; i < count; i++)
            {
                var label = CreateRect($"DoorLabel_{i + 1:00}", generatedRoot).gameObject.AddComponent<TextMeshProUGUI>();
                label.text = "開";
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Normal;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;
                cars[i].label = label;
            }
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        private void ClearGenerated()
        {
            cars.Clear();
            if (generatedRoot == null) return;
            GameObject root = generatedRoot.gameObject;
            generatedRoot = null;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }

        private void OnValidate()
        {
            spacingX = Mathf.Max(0f, spacingX);
            spriteSize = new Vector2(Mathf.Max(1f, spriteSize.x), Mathf.Max(1f, spriteSize.y));
            labelFontSize = Mathf.Max(1f, labelFontSize);
            labelSize = new Vector2(Mathf.Max(1f, labelSize.x), Mathf.Max(1f, labelSize.y));
        }
    }
}
