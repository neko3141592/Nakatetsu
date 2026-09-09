using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Tims.Presentation.Indicators
{
    public enum BrakeNotchCellState
    {
        On,
        Off,
        Blank
    }

    [DisallowMultipleComponent]
    public sealed class BrakeNotchCell : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private BrakeNotchCellState state = BrakeNotchCellState.Off;

        [SerializeField, Min(1)]
        private int notch = 1;

        [Header("Objects")]
        [SerializeField]
        private Image background;

        [SerializeField]
        private TMP_Text bText;

        [SerializeField]
        private TMP_Text notchText;

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

        public BrakeNotchCellState State => state;
        public int Notch => notch;

        private void Awake()
        {
            CacheChildComponents();
            ApplyVisuals();
        }

        private void OnValidate()
        {
            notch = Mathf.Max(1, notch);
            CacheChildComponents();
            ApplyVisuals();
        }

        public void SetState(BrakeNotchCellState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            ApplyVisuals();
        }

        public void SetNotch(int newNotch)
        {
            newNotch = Mathf.Max(1, newNotch);
            if (notch == newNotch)
            {
                return;
            }

            notch = newNotch;
            ApplyVisuals();
        }

        public void Set(BrakeNotchCellState newState, int newNotch)
        {
            state = newState;
            notch = Mathf.Max(1, newNotch);
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (background == null || bText == null || notchText == null)
            {
                return;
            }

            bool isBlank = state == BrakeNotchCellState.Blank;
            bool isOn = state == BrakeNotchCellState.On;

            background.color = isOn || isBlank
                ? backgroundOnColor
                : backgroundOffColor;

            Color textColor = isOn ? textOnColor : textOffColor;
            bText.color = textColor;
            notchText.color = textColor;

            bText.enabled = !isBlank;
            notchText.enabled = !isBlank;
            notchText.text = notch.ToString();
        }

        private void CacheChildComponents()
        {
            background ??= FindChildComponent<Image>("Background");
            bText ??= FindChildComponent<TMP_Text>("B_text");
            notchText ??= FindChildComponent<TMP_Text>("Notch_text");
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
