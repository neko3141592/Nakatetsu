using System.Globalization;
using Nakatetsu.Train.Equipment.Tims.Traction;
using Nakatetsu.Train.Equipment.Tims.Communication;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Gauges
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TimsCurrentGauge : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private bool useManualCurrent;
        [Tooltip("表示確認用。正が力行、負が回生。単位A。")]
        [SerializeField] private float manualCurrentA;
        [SerializeField, Min(1f)] private float maximumCurrentA = 1500f;
        [SerializeField, Min(1f)] private float height = 350f;

        [Header("Regeneration (Left)")]
        [SerializeField] private Image regenBar;
        [SerializeField] private VerticalGaugeScale regenScale;
        [SerializeField] private float regenPositionX = -55f;
        [SerializeField, Min(0f)] private float regenWidth = 45f;

        [Header("Traction (Right)")]
        [SerializeField] private Image powerBar;
        [SerializeField] private float powerPositionX = 10f;
        [SerializeField, Min(0f)] private float powerWidth = 45f;
        [SerializeField] private TMP_Text currentLabel;

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void OnDisable()
        {
            if (regenBar != null) regenBar.enabled = false;
            if (powerBar != null) powerBar.enabled = false;
            if (currentLabel != null) currentLabel.text = "--";
        }

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            Refresh();
        }

        public void Refresh()
        {
            float current = manualCurrentA;
            bool available = useManualCurrent;
            if (!useManualCurrent)
            {
                available = tims != null && tims.isActiveAndEnabled &&
                    tims.MasterBus.TryGetFloat(TimsCurrentController.SignedMotorCurrentAKey, out current);
            }
            available &= IsFinite(current) && IsFinite(maximumCurrentA) && maximumCurrentA > 0f;
            // 数値とバーを同じ5A刻みに丸める。TIMSの測定値自体は変更しない。
            current = available ? Mathf.Round(current / 5f) * 5f : 0f;
            float plotHeight = IsFinite(height) ? Mathf.Max(1f, height) : 1f;
            float bottom = -plotHeight * 0.5f;
            UpdateScale(regenScale, bottom, plotHeight);
            UpdateBar(regenBar, regenPositionX, regenWidth, bottom,
                available ? plotHeight * Mathf.Clamp01(-current / maximumCurrentA) : 0f, available);
            UpdateBar(powerBar, powerPositionX, powerWidth, bottom,
                available ? plotHeight * Mathf.Clamp01(current / maximumCurrentA) : 0f, available);
            if (currentLabel != null)
            {
                currentLabel.text = available ? current.ToString("0", CultureInfo.InvariantCulture) : "--";
                currentLabel.alignment = TextAlignmentOptions.Right;
            }
        }

        private void UpdateScale(VerticalGaugeScale scale, float bottom, float length)
        {
            if (scale == null) return;
            scale.SetMaximum(maximumCurrentA);
            RectTransform rect = scale.rectTransform;
            rect.anchorMin = new Vector2(rect.anchorMin.x, 0.5f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
            rect.pivot = new Vector2(rect.pivot.x, 0f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, length);
        }

        private static void UpdateBar(Image bar, float x, float width, float bottom, float length, bool available)
        {
            if (bar == null) return;
            bar.enabled = available;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, bottom);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, width), length);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
