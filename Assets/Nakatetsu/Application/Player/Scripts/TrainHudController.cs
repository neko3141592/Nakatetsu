using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Integration;
using TMPro;
using UnityEngine;

namespace Nakatetsu.Application.Player
{
    [DisallowMultipleComponent]
    public sealed class TrainHudController : MonoBehaviour
    {
        [SerializeField] private TrainFocusController focusController;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text notchText;
        [SerializeField] private TMP_Text reverserText;
        [SerializeField] private TMP_Text trainInfoText;

        [Header("Notch Colors")]
        [SerializeField] private Color powerColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color brakeColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color emergencyBrakeColor = new Color(1f, 0.2f, 0.2f);

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void LateUpdate()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            TrainStatusController status = GetFocusedStatus();
            if (status == null)
            {
                SetText(speedText, "-- km/h");
                SetNotchText("--", Color.white);
                SetText(reverserText, "--");
                SetText(trainInfoText, string.Empty);
                return;
            }

            if (status.TryGetSpeedMps(out float speedMps))
            {
                SetText(speedText, $"{speedMps * 3.6f:0.0} km/h");
            }
            else
            {
                SetText(speedText, "-- km/h");
            }

            if (status.TryGetActiveCabControls(out TrainCabControls controls))
            {
                UpdateNotch(controls);
                SetText(reverserText, GetReverserText(controls.ReverserPosition));
            }
            else
            {
                SetNotchText("--", Color.white);
                SetText(reverserText, "--");
            }

            string trainId = string.Empty;
            if (status.TryGetTrainId(out string currentTrainId))
            {
                trainId = currentTrainId;
            }

            SetText(trainInfoText, trainId);
        }

        private TrainStatusController GetFocusedStatus()
        {
            if (focusController == null || !focusController.isActiveAndEnabled)
            {
                return null;
            }

            var train = focusController.FocusedTrain;
            if (train == null || !train.isActiveAndEnabled || train.Status == null ||
                !train.Status.isActiveAndEnabled)
            {
                return null;
            }

            return train.Status;
        }

        private void UpdateNotch(TrainCabControls controls)
        {
            if (controls.IsEmergencyBrake)
            {
                SetNotchText("EB", emergencyBrakeColor);
                return;
            }

            if (controls.BrakePosition > 0)
            {
                SetNotchText($"B{controls.BrakePosition}", brakeColor);
                return;
            }

            if (controls.PowerPosition > 0)
            {
                SetNotchText($"P{controls.PowerPosition}", powerColor);
                return;
            }

            SetNotchText("N", neutralColor);
        }

        private void SetNotchText(string value, Color color)
        {
            if (notchText == null)
            {
                return;
            }

            notchText.text = value;
            notchText.color = color;
        }

        private static string GetReverserText(ReverserPosition position)
        {
            switch (position)
            {
                case ReverserPosition.Forward:
                    return "前";
                case ReverserPosition.Neutral:
                    return "中";
                case ReverserPosition.Reverse:
                    return "後";
                default:
                    return "--";
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
