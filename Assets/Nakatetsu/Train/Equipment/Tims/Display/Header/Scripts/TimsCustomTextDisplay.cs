using System;
using System.Globalization;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using TMPro;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Readout
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/Custom Text Display")]
    public sealed class TimsCustomTextDisplay : MonoBehaviour
    {
        [Serializable]
        public sealed class ValueBinding
        {
            public TimsBusTarget busTarget = TimsBusTarget.Master;
            [Tooltip("LocalBusを読む号車。先頭車が1です。")]
            [Min(1)] public int carNumber = 1;
            public string deviceName;
            public string itemName;
        }

        [Header("Source")]
        [Tooltip("未指定なら親のTIMS、または所属TrainRoot内のTIMSを自動取得します。")]
        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private TMP_Text outputText;

        [Header("Content")]
        [SerializeField] private string label;
        [SerializeField] private ValueBinding value = new();

        [Header("Colors")]
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color valueColor = Color.white;

        private void Reset()
        {
            outputText = GetComponent<TMP_Text>();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) Render("－");
        }
#endif

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            Refresh();
        }

        public void Refresh()
        {
            ResolveTims();
            Render(TryReadValue(out string text) ? text : "－");
        }

        private void ResolveTims()
        {
            if (tims != null) return;
            tims = GetComponentInParent<TimsCommunicationController>(true);
            if (tims != null) return;

            TrainRoot train = GetComponentInParent<TrainRoot>(true);
            if (train != null) tims = train.GetComponentInChildren<TimsCommunicationController>(true);
        }

        private bool TryReadValue(out string text)
        {
            text = null;
            if (tims == null || !tims.isActiveAndEnabled || value == null ||
                string.IsNullOrWhiteSpace(value.deviceName) || string.IsNullOrWhiteSpace(value.itemName))
                return false;

            TimsBusState bus;
            if (value.busTarget == TimsBusTarget.Master)
                bus = tims.MasterBus;
            else if (!tims.TryGetLocalBus(value.carNumber - 1, out bus))
                return false;

            if (bus == null || !bus.Tags.TryGetValue(new TimsTagKey(value.deviceName, value.itemName), out TimsValue data))
                return false;

            text = data.Type switch
            {
                TimsValueType.Bool => data.BoolValue ? "True" : "False",
                TimsValueType.Int => data.IntValue.ToString(CultureInfo.InvariantCulture),
                TimsValueType.Float => data.FloatValue.ToString(CultureInfo.InvariantCulture),
                TimsValueType.String => data.StringValue ?? string.Empty,
                TimsValueType.IntArray => string.Join(", ", data.IntArrayValue),
                TimsValueType.FloatArray => string.Join(", ", Array.ConvertAll(data.FloatArrayValue,
                    number => number.ToString(CultureInfo.InvariantCulture))),
                TimsValueType.StringArray => string.Join(", ", data.StringArrayValue),
                _ => null
            };
            return text != null;
        }

        private void Render(string displayedValue)
        {
            if (outputText == null) outputText = GetComponent<TMP_Text>();
            if (outputText == null) return;

            outputText.richText = true;
            string valuePart = $"<color=#{ColorUtility.ToHtmlStringRGBA(valueColor)}><noparse>{displayedValue}</noparse></color>";
            string result = string.IsNullOrEmpty(label)
                ? valuePart
                : $"<color=#{ColorUtility.ToHtmlStringRGBA(labelColor)}><noparse>{label}</noparse></color> {valuePart}";
            if (outputText.text != result) outputText.text = result;
        }
    }
}
