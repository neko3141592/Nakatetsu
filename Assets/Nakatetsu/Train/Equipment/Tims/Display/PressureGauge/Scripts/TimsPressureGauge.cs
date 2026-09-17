using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Communication;
using TMPro;
using System.Globalization;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Gauges
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TimsPressureGauge : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController tims;
        [Tooltip("BC圧を読む車両。0が先頭車です。")]
        [SerializeField, Min(0)] private int carIndex;
        [Tooltip("バーが最大まで伸びる圧力（kPa）。")]
        [SerializeField, Min(1f)] private float maximumPressureKPa = 500f;
        [SerializeField] private Image bar;
        [SerializeField] private VerticalGaugeScale scale;

        [Header("Layout")]
        [SerializeField, Min(1f)] private float height = 330f;

        [Header("BC Bar")]
        [SerializeField, Min(0f)] private float bcBarWidth = 40f;
        [SerializeField] private float bcBarPositionX = 30f;
        [SerializeField] private TMP_Text bcLabel;

        [Header("MR Bar")]
        [SerializeField] private Image mrBar;
        [SerializeField, Min(0f)] private float mrBarWidth = 60f;
        [SerializeField] private float mrBarPositionX = 70f;
        [SerializeField] private TMP_Text mrLabel;
        [Tooltip("MR装置が未実装の間に表示確認用の圧力を使う。")]
        [SerializeField] private bool useManualMrPressure = true;
        [SerializeField, Min(0f)] private float manualMrPressureKPa = 700f;
        [Tooltip("MR圧を取得するLocalBusの装置名と項目名。")]
        [SerializeField] private string mrDeviceName = "MR";
        [SerializeField] private string mrItemName = "PressureKPa";

        [Header("MR Normal Range")]
        [SerializeField] private Image mrNormalRange;
        [SerializeField, Min(0f)] private float mrNormalMinimumKPa = 600f;
        [SerializeField, Min(0f)] private float mrNormalMaximumKPa = 800f;
        [SerializeField, Min(0f)] private float mrNormalRangeWidth = 80f;
        [Tooltip("正常範囲の計算位置からの移動量。Xは右、Yは上が正。")]
        [SerializeField] private Vector2 mrNormalRangeOffset;
        [SerializeField] private Color mrNormalRangeColor = new Color(1f, 0.5f, 0.6f);

        [Header("Labels")]
        [SerializeField, Min(0f)] private float labelGap = 8f;
        [SerializeField, Min(1f)] private float labelHeight = 28f;

        [Header("Pressure Values")]
        [SerializeField] private TMP_Text bcPressureLabel;
        [SerializeField] private TMP_Text mrPressureLabel;

        private float pressureRatio = 1f;
        private float mrPressureRatio;

        public void Configure(TimsCommunicationController source, int localCarIndex)
        {
            tims = source;
            carIndex = Mathf.Max(0, localCarIndex);
            Refresh();
        }

        private void OnEnable() => LateUpdate();

        private void LateUpdate()
        {
            if (Application.isPlaying) Refresh();
            else
            {
                mrPressureRatio = NormalizePressure(manualMrPressureKPa);
                if (mrBar != null) mrBar.enabled = useManualMrPressure;
                UpdatePressureLabel(bcPressureLabel, false, 0f, bar != null ? bar.color : Color.white);
                UpdatePressureLabel(mrPressureLabel, useManualMrPressure && IsPressure(manualMrPressureKPa), manualMrPressureKPa, mrNormalRangeColor);
                UpdateLayout();
            }
        }

        private void OnDisable()
        {
            if (bar != null) bar.enabled = false;
            if (mrBar != null) mrBar.enabled = false;
        }

        public void Refresh()
        {
            bool validScale = IsFinite(maximumPressureKPa) && maximumPressureKPa > 0f;
            TimsBusState bus = null;
            if (tims != null && tims.isActiveAndEnabled) tims.TryGetLocalBus(carIndex, out bus);
            float bcPressure = 0f;
            bool bcAvailable = validScale && bus != null &&
                bus.TryGetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey, out bcPressure) && IsPressure(bcPressure);
            if (bar != null) bar.enabled = bcAvailable;
            pressureRatio = bcAvailable ? NormalizePressure(bcPressure) : 0f;

            float mrPressure = manualMrPressureKPa;
            bool mrAvailable = useManualMrPressure;
            if (!useManualMrPressure)
            {
                mrAvailable = bus != null && !string.IsNullOrWhiteSpace(mrDeviceName) &&
                    !string.IsNullOrWhiteSpace(mrItemName) &&
                    bus.TryGetFloat(new TimsTagKey(mrDeviceName, mrItemName), out mrPressure);
            }
            mrAvailable &= validScale && IsPressure(mrPressure);
            if (mrBar != null) mrBar.enabled = mrAvailable;
            mrPressureRatio = mrAvailable ? NormalizePressure(mrPressure) : 0f;
            UpdatePressureLabel(bcPressureLabel, bcAvailable, bcPressure, bar != null ? bar.color : Color.white);
            UpdatePressureLabel(mrPressureLabel, mrAvailable, mrPressure, mrNormalRangeColor);
            UpdateLayout();
        }

        private static void UpdatePressureLabel(TMP_Text label, bool available, float pressure, Color color)
        {
            if (label == null) return;
            // 数値表示だけを5 kPa刻みに四捨五入する。
            float displayPressureKPa = Mathf.Floor(pressure / 5f + 0.5f) * 5f;
            label.text = available ? displayPressureKPa.ToString("0", CultureInfo.InvariantCulture) : "--";
            label.alignment = TextAlignmentOptions.Right;
            label.color = color;
        }

        private float NormalizePressure(float pressure)
        {
            return IsPressure(pressure) && IsFinite(maximumPressureKPa) && maximumPressureKPa > 0f
                ? Mathf.Clamp01(pressure / maximumPressureKPa) : 0f;
        }

        private static bool IsPressure(float pressure) => IsFinite(pressure) && pressure >= 0f;

        private void UpdateLayout()
        {
            float totalHeight = Mathf.Max(1f, height);
            // 上下の数字が収まる余白を除いた高さを、目盛りとバーで共有する。
            float padding = scale != null ? Mathf.Min(scale.EndPadding, totalHeight * 0.5f) : 0f;
            float plotHeight = totalHeight - padding * 2f;
            float bottom = -totalHeight * 0.5f + padding;

            if (scale != null)
            {
                scale.SetMaximum(maximumPressureKPa);
                SetVerticalLayout(scale.rectTransform, bottom, plotHeight);
            }
            if (bar != null)
            {
                SetBarLayout(bar.rectTransform, bcBarPositionX, bcBarWidth, bottom, plotHeight * pressureRatio);
            }
            if (mrBar != null)
            {
                SetBarLayout(mrBar.rectTransform, mrBarPositionX, mrBarWidth, bottom, plotHeight * mrPressureRatio);
            }
            if (mrNormalRange != null)
            {
                bool valid = IsPressure(mrNormalMinimumKPa) && IsPressure(mrNormalMaximumKPa) &&
                    mrNormalMaximumKPa >= mrNormalMinimumKPa && IsFinite(maximumPressureKPa) && maximumPressureKPa > 0f;
                mrNormalRange.enabled = valid;
                mrNormalRange.color = mrNormalRangeColor;
                float lower = NormalizePressure(mrNormalMinimumKPa);
                float upper = NormalizePressure(mrNormalMaximumKPa);
                // 実圧バーの中心に正常範囲を揃える。
                float rangeWidth = Mathf.Max(0f, mrNormalRangeWidth);
                float x = mrBarPositionX + (Mathf.Max(0f, mrBarWidth) - rangeWidth) * 0.5f;
                SetBarLayout(mrNormalRange.rectTransform, x + mrNormalRangeOffset.x, rangeWidth,
                    bottom + plotHeight * lower + mrNormalRangeOffset.y, plotHeight * Mathf.Max(0f, upper - lower));
            }
            UpdateLabel(bcLabel, bar, "BC", bcBarPositionX, bcBarWidth, bottom + plotHeight);
            UpdateLabel(mrLabel, mrBar, "MR", mrBarPositionX, mrBarWidth, bottom + plotHeight);
            if (mrLabel != null) mrLabel.color = mrNormalRangeColor;
        }

        private static void SetBarLayout(RectTransform rect, float x, float width, float bottom, float length)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, bottom);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, width), length);
        }

        private void UpdateLabel(TMP_Text label, Image image, string title, float x, float width, float top)
        {
            if (label == null || image == null) return;
            label.text = title;
            label.color = image.color;
            label.alignment = TextAlignmentOptions.Center;
            SetBarLayout(label.rectTransform, x, width, top + labelGap, labelHeight);
        }

        private static void SetVerticalLayout(RectTransform rect, float bottom, float length)
        {
            // 横位置と幅は各RectTransformの設定を維持する。
            rect.anchorMin = new Vector2(rect.anchorMin.x, 0.5f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
            rect.pivot = new Vector2(rect.pivot.x, 0f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, length);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
