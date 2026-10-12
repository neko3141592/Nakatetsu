using System;
using System.Collections.Generic;
using System.Globalization;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.TrainStatus;
using Nakatetsu.Train.Equipment.Tims.Traction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.BrakeStatus
{
    /// <summary>既存の列車表示と同じ車両中心に、BC圧と回生指令・実績を表示する。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/Brake Status Display")]
    public sealed class TimsBrakeStatusDisplay : MonoBehaviour
    {
        private static readonly Color patternColor = new Color32(238, 174, 0, 255);
        private static readonly Color feedbackColor = new Color32(48, 156, 255, 255);
        private static readonly Color missingColor = new Color32(105, 110, 127, 255);

        [Header("Source")]
        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private TimsTrainStatusDisplay trainStatus;
        [Tooltip("編集用の見本。実行時は生成した表示に置き換えます。")]
        [SerializeField] private GameObject editorSample;

        [Header("Plots")]
        [SerializeField] private RectTransform bcPlot;
        [SerializeField] private RectTransform regenPlot;
        [SerializeField, Min(1f)] private float barWidth = 28f;
        [SerializeField, Min(1f)] private float bcMaximumKPa = 800f;
        [SerializeField, Min(1f)] private float regenMaximumKN = 100f;

        [SerializeField, HideInInspector] private RectTransform generatedBcRoot;
        [SerializeField, HideInInspector] private RectTransform generatedRegenRoot;
        private readonly List<CarVisual> cars = new();

        private sealed class ReadingVisual
        {
            public Image bar;
            public TMP_Text valueLabel;
        }

        private sealed class CarVisual
        {
            public ReadingVisual bc;
            public ReadingVisual regenPattern;
            public ReadingVisual regenFeedback;
        }

        public int GeneratedCarCount => cars.Count;
        public RectTransform GeneratedBcRoot => generatedBcRoot;
        public RectTransform GeneratedRegenRoot => generatedRegenRoot;

        private void OnEnable()
        {
            if (Application.isPlaying && editorSample != null) editorSample.SetActive(false);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void OnDisable()
        {
            if (generatedBcRoot != null) generatedBcRoot.gameObject.SetActive(false);
            if (generatedRegenRoot != null) generatedRegenRoot.gameObject.SetActive(false);
        }

        private void OnDestroy() => ClearGenerated();

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            if (trainStatus != null) trainStatus.SetTimsSource(source);
            Refresh();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            TrainRoot owner = ResolveOwner();
            ConsistDefinitionAsset definition = owner != null ? owner.ConsistDefinition : null;
            if (trainStatus == null || bcPlot == null || regenPlot == null || definition == null)
            {
                ClearGenerated();
                return;
            }

            // LateUpdateのコンポーネント順に依存せず、編成長・配置の変更を同じ描画で反映する。
            trainStatus.Refresh();
            int count = trainStatus.GeneratedCarCount;
            if (count == 0)
            {
                ClearGenerated();
                return;
            }
            if (NeedsRebuild(count)) Build(count);
            if (editorSample != null) editorSample.SetActive(false);
            generatedBcRoot.gameObject.SetActive(true);
            generatedRegenRoot.gameObject.SetActive(true);
            generatedBcRoot.pivot = bcPlot.pivot;
            generatedRegenRoot.pivot = regenPlot.pivot;

            bool sourceAvailable = tims != null && tims.isActiveAndEnabled && owner.isActiveAndEnabled;
            float[] regenTargetsN = null;
            bool hasRegenTargets = sourceAvailable && tims.MasterBus.TryGetFloatArray(
                TimsBrakeController.TargetRegenForcesNKey, out regenTargetsN);
            float width = IsFinite(barWidth) ? Mathf.Max(0f, barWidth) : 0f;
            for (int i = 0; i < count; i++)
            {
                CarVisual visual = cars[i];
                CarDefinitionAsset car = i < definition.CarCount ? definition.cars[i] : null;
                bool motor = car != null && (car.motorCount > 0 || car.carType == CarType.M ||
                    car.carType == CarType.Mc1 || car.carType == CarType.Mc2);
                bool hasBcPosition = trainStatus.TryGetCarCenter(i, bcPlot, out Vector2 bcPosition);
                bool hasRegenPosition = trainStatus.TryGetCarCenter(i, regenPlot, out Vector2 regenPosition);
                SetActive(visual.bc, car != null && hasBcPosition && IsFinite(bcPosition.x));
                SetActive(visual.regenPattern, motor && hasRegenPosition && IsFinite(regenPosition.x));
                SetActive(visual.regenFeedback, motor && hasRegenPosition && IsFinite(regenPosition.x));
                if (car == null) continue;

                TimsBusState localBus = null;
                if (sourceAvailable) tims.TryGetLocalBus(i, out localBus);
                float pressureKPa = 0f;
                bool hasPressure = localBus != null && localBus.TryGetFloat(
                    BrakeControlDeviceTimsBusSource.PressureKPaKey, out pressureKPa);
                UpdateReading(visual.bc, bcPlot, bcPosition.x, bcPosition.x, 8f, width, hasPressure,
                    pressureKPa, bcMaximumKPa, true);

                // 回生装置がない付随車はバーと数値を表示しない。
                if (!motor) continue;

                bool driveAvailable = sourceAvailable && localBus != null && localBus.TryGetBool(
                    TimsTractionBusSource.IsAvailableKey, out bool available) && available;
                float patternKN = 0f;
                bool hasPattern = driveAvailable && hasRegenTargets && i < regenTargetsN.Length;
                if (hasPattern) patternKN = regenTargetsN[i] / 1000f;
                float feedbackN = 0f;
                bool hasFeedback = driveAvailable && localBus.TryGetFloat(
                    TimsTractionBusSource.ActualRegenForceNKey, out feedbackN);
                UpdateReading(visual.regenPattern, regenPlot, regenPosition.x - width * .5f,
                    regenPosition.x, 8f, width, hasPattern, patternKN, regenMaximumKN, false);
                UpdateReading(visual.regenFeedback, regenPlot, regenPosition.x + width * .5f,
                    regenPosition.x, 30f, width, hasFeedback, feedbackN / 1000f, regenMaximumKN, false);
            }
        }

        private static void SetActive(ReadingVisual visual, bool active)
        {
            visual.bar.gameObject.SetActive(active);
            visual.valueLabel.gameObject.SetActive(active);
        }

        private static void UpdateReading(ReadingVisual visual, RectTransform plot, float barCenterX,
            float labelCenterX, float labelOffsetY, float width, bool hasValue, float value,
            float maximum, bool pressure)
        {
            bool valid = hasValue && IsFinite(value) && value >= 0f && IsFinite(maximum) && maximum > 0f;
            float plotHeight = Mathf.Max(0f, plot.rect.height);
            float length = valid ? plotHeight * Mathf.Clamp01(value / maximum) : 0f;
            visual.bar.enabled = valid;
            visual.valueLabel.enabled = true;
            visual.valueLabel.text = !valid ? "--" :
                pressure ? FormatPressureKPa(value) : FormatRegenKN(value);
            visual.valueLabel.color = valid ? visual.bar.color : missingColor;
            RectTransform bar = visual.bar.rectTransform;
            bar.anchorMin = bar.anchorMax = plot.pivot;
            bar.pivot = new Vector2(.5f, 0f);
            bar.anchoredPosition = new Vector2(barCenterX, plot.rect.yMin);
            bar.sizeDelta = new Vector2(width, length);
            RectTransform label = visual.valueLabel.rectTransform;
            label.anchorMin = label.anchorMax = plot.pivot;
            label.pivot = new Vector2(.5f, 1f);
            label.anchoredPosition = new Vector2(labelCenterX, plot.rect.yMin - labelOffsetY);
            label.sizeDelta = new Vector2(70f, pressure ? 24f : 22f);
        }

        public static string FormatPressureKPa(float pressureKPa) => IsNonNegativeFinite(pressureKPa)
            ? Math.Round(pressureKPa, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
            : "--";

        public static string FormatRegenKN(float forceKN) => IsNonNegativeFinite(forceKN)
            ? Math.Round(forceKN, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
            : "--";

        private TrainRoot ResolveOwner()
        {
            if (tims == null)
            {
                tims = GetComponentInParent<TimsCommunicationController>(true);
                if (tims == null)
                {
                    var train = GetComponentInParent<TrainRoot>(true);
                    if (train != null) tims = train.GetComponentInChildren<TimsCommunicationController>(true);
                }
            }
            return tims != null ? tims.GetComponentInParent<TrainRoot>(true) : GetComponentInParent<TrainRoot>(true);
        }

        private bool NeedsRebuild(int count)
        {
            if (generatedBcRoot == null || generatedRegenRoot == null || cars.Count != count ||
                generatedBcRoot.parent != bcPlot || generatedRegenRoot.parent != regenPlot) return true;
            foreach (CarVisual car in cars)
                if (!IsPresent(car.bc) || !IsPresent(car.regenPattern) || !IsPresent(car.regenFeedback)) return true;
            return false;
        }

        private static bool IsPresent(ReadingVisual visual) => visual?.bar != null && visual.valueLabel != null;

        private void Build(int count)
        {
            ClearGenerated();
            generatedBcRoot = CreateRoot("GeneratedBC", bcPlot);
            generatedRegenRoot = CreateRoot("GeneratedRegen", regenPlot);
            for (int i = 0; i < count; i++)
            {
                cars.Add(new CarVisual
                {
                    bc = CreateReading($"BC_{i + 1:00}", generatedBcRoot, patternColor, 20f),
                    regenPattern = CreateReading($"PTN_{i + 1:00}", generatedRegenRoot, patternColor, 18f),
                    regenFeedback = CreateReading($"FB_{i + 1:00}", generatedRegenRoot, feedbackColor, 18f)
                });
            }
        }

        private ReadingVisual CreateReading(string name, RectTransform parent, Color color, float fontSize)
        {
            var bar = CreateRect(name, parent).gameObject.AddComponent<Image>();
            bar.color = color;
            bar.raycastTarget = false;
            var label = CreateRect(name + "_Value", parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "--";
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = missingColor;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return new ReadingVisual { bar = bar, valueLabel = label };
        }

        private RectTransform CreateRoot(string name, RectTransform plot)
        {
            RectTransform root = CreateRect(name, plot);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = plot.pivot;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            return root;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private void ClearGenerated()
        {
            cars.Clear();
            ClearRoot(ref generatedBcRoot);
            ClearRoot(ref generatedRegenRoot);
        }

        private static void ClearRoot(ref RectTransform generatedRoot)
        {
            if (generatedRoot == null) return;
            GameObject root = generatedRoot.gameObject;
            generatedRoot = null;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsNonNegativeFinite(float value) => IsFinite(value) && value >= 0f;
    }
}
