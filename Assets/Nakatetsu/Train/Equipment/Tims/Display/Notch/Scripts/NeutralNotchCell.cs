using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Indicators
{
    public enum NeutralNotchCellState
    {
        On,
        Off
    }

    [DisallowMultipleComponent]
    public sealed class NeutralNotchCell : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private NeutralNotchCellState state = NeutralNotchCellState.Off;

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

        public NeutralNotchCellState State => state;

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

        public void SetState(NeutralNotchCellState newState)
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

            bool isOn = state == NeutralNotchCellState.On;

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
