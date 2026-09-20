using System;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Indicators
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Nakatetsu/Tims/Bool Indicator Display")]
    public sealed class TimsBoolIndicatorDisplay : MonoBehaviour
    {
        [Serializable]
        public sealed class IndicatorSettings
        {
            public TimsBusTarget busTarget = TimsBusTarget.Master;
            [Tooltip("LocalBusの号車。1が先頭車。MasterBusでは使用しません。")]
            [Min(1)] public int carNumber = 1;
            public string deviceName;
            public string itemName;
            public Color backgroundOnColor = Color.green;
            public Color labelOnColor = Color.black;
            public string label;
        }

        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private TimsBoolIndicator indicatorPrefab;
        [SerializeField] private IndicatorSettings[] indicators = Array.Empty<IndicatorSettings>();

        [Header("Layout")]
        [Tooltip("インジケーターの中心間の縦間隔（Canvas単位）。")]
        [SerializeField, Min(0f)] private float verticalSpacing = 60f;
        [Tooltip("インジケーターの中心間の横間隔（Canvas単位）。")]
        [SerializeField, Min(0f)] private float horizontalSpacing = 210f;
        [Tooltip("縦にこの個数を並べたら右の列へ折り返します。")]
        [SerializeField, Min(1)] private int indicatorsPerColumn = 4;

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
        }

        private void Awake()
        {
            if (indicators == null || indicators.Length == 0) return;
            if (indicatorPrefab == null || !(indicatorPrefab.transform is RectTransform))
            {
                Debug.LogWarning("BoolIndicatorのRectTransform付きPrefabを指定してください。", this);
                return;
            }

            int rows = Mathf.Max(1, indicatorsPerColumn);
            for (int index = 0; index < indicators.Length; index++)
            {
                IndicatorSettings settings = indicators[index];
                if (settings == null) continue;

                TimsBoolIndicator indicator = Instantiate(indicatorPrefab, transform, false);
                indicator.name = $"Indicator {index + 1} ({settings.label})";
                RectTransform rect = (RectTransform)indicator.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    index / rows * Mathf.Max(0f, horizontalSpacing),
                    -(index % rows) * Mathf.Max(0f, verticalSpacing));
                indicator.Configure(tims, settings.busTarget, settings.carNumber - 1,
                    settings.deviceName, settings.itemName, settings.backgroundOnColor,
                    settings.labelOnColor, settings.label);
            }
        }
    }
}
