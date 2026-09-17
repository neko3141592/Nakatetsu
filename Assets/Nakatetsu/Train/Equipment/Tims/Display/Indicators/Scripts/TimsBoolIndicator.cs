using UnityEngine;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine.UI;
using TMPro;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Indicators
{
    public enum TimsBusTarget
    {
        Master,
        Local
    }

    public sealed class TimsBoolIndicator : MonoBehaviour {

        [Header("Tims")]
        [SerializeField]
        private TimsCommunicationController tims;

        [Header("Binding")]
        [SerializeField]
        private string deviceName;

        [SerializeField]
        private string itemName;

        [Header("Bus")]
        [SerializeField]
        private TimsBusTarget target;

        [SerializeField, Min(0)]
        private int localCarIndex;

        [Header("Objects")]
        [SerializeField]
        private Image background;

        [SerializeField]
        private TMP_Text labelText;

        [Header("Colors")]
        [SerializeField]
        private Color backgroundOnColor = Color.green;

        [SerializeField]
        private Color backgroundOffColor = Color.black;

        [SerializeField]
        private Color labelOnColor = Color.black;

        [SerializeField]
        private Color labelOffColor = Color.gray;

        private void Awake()
        {
            CacheObjects();
            Refresh();
        }

        public void Configure(TimsCommunicationController source, TimsBusTarget busTarget, int carIndex,
            string device, string item, Color onBackground, Color onText, string label)
        {
            tims = source;
            target = busTarget;
            localCarIndex = carIndex;
            deviceName = device;
            itemName = item;
            backgroundOnColor = onBackground;
            labelOnColor = onText;
            CacheObjects();
            if (labelText != null) labelText.text = label;
            Refresh();
        }

        private void CacheObjects()
        {
            if (background == null) background = GetComponentInChildren<Image>(true);
            if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);
        }

        private TimsBusState GetBus()
        {
            if (tims == null || !tims.isActiveAndEnabled) return null;
            if (target == TimsBusTarget.Master) return tims.MasterBus;
            return target == TimsBusTarget.Local && tims.TryGetLocalBus(localCarIndex, out var localBus)
                ? localBus : null;
        }

        private void LateUpdate() => Refresh();
        private void OnDisable() => Apply(false);

        public void Refresh()
        {
            TimsBusState bus = GetBus();
            bool isOn = bus != null && !string.IsNullOrWhiteSpace(deviceName) &&
                !string.IsNullOrWhiteSpace(itemName) &&
                bus.TryGetBool(new TimsTagKey(deviceName, itemName), out bool value) && value;
            Apply(isOn);
        }

        private void Apply(bool isOn)
        {
            if (background != null) background.color = isOn ? backgroundOnColor : backgroundOffColor;
            if (labelText != null) labelText.color = isOn ? labelOnColor : labelOffColor;
        }
    }
}
