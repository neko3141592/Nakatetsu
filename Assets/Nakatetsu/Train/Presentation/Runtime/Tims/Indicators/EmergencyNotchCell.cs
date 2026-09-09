using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Tims.Presentation.Indicators
{
    public enum EmergencyNotchCellState
    {
        On,
        Off
    }

    [DisallowMultipleComponent]
    public sealed class EmergencyNotchCell : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private EmergencyNotchCellState state = EmergencyNotchCellState.Off;

        [Header("Objects")]
        [SerializeField]
        private Image background;

        private TMP_Text labelText;

        [Header("On Colors")]
        [SerializeField]
        private Color backgroundOnColor = Color.cyan;

        [SerializeField]
        private Color textOnColor = Color.black;

        [Header("Off Colors")]
        [SerializeField]
        private Color backgroundOffColor = Color.black;

        [SerializeField]
        private Color textOffColor = Color.gray;

        public EmergencyNotchCellState State => state;

        private void Awake()
        {
            CacheChildComponents();
            ApplyVisuals();
        }

        private void OnValidate()
        {
            CacheChildComponents();
            ApplyVisuals();
        }

        public void SetState(EmergencyNotchCellState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (background == null || labelText == null)
            {
                return;
            }

            bool isOn = state == EmergencyNotchCellState.On;

            background.color = isOn
                ? backgroundOnColor
                : backgroundOffColor;
            labelText.color = isOn ? textOnColor : textOffColor;
        }

        private void CacheChildComponents()
        {
            background ??= FindChildComponent<Image>("Background");
            labelText ??= GetComponentInChildren<TMP_Text>(true);
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName && child.TryGetComponent(out T component))
                {
                    return component;
                }
            }

            return null;
        }
    }
}
