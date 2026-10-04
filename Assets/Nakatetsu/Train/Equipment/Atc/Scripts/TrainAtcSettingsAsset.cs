using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    [CreateAssetMenu(
        fileName = "TrainAtcSettings",
        menuName = "Nakatetsu/Train/ATC Settings")]
    public sealed class TrainAtcSettingsAsset : ScriptableObject
    {
        [SerializeField] private TrainAtcSettings settings = new();

        public TrainAtcSettings Settings => settings;

        public void ApplyTo(TrainAtcSettings destination)
        {
            if (destination == null) return;
            destination.CopyFrom(settings);
        }

        private void OnValidate()
        {
            settings ??= new TrainAtcSettings();
            settings.noSignalTimeoutSeconds = Mathf.Max(0f, settings.noSignalTimeoutSeconds);
            settings.maximumSamplingIntervalM = Mathf.Max(0.01f, settings.maximumSamplingIntervalM);
            settings.patternApproachWarningTimeSeconds = Mathf.Max(0f, settings.patternApproachWarningTimeSeconds);
            settings.serviceDecelerationMps2 = Mathf.Max(0f, settings.serviceDecelerationMps2);
            settings.emergencyDecelerationMps2 = Mathf.Max(0f, settings.emergencyDecelerationMps2);
            settings.serviceStopMarginM = Mathf.Max(0f, settings.serviceStopMarginM);
            settings.emergencyStopMarginM = Mathf.Max(0f, settings.emergencyStopMarginM);
            settings.normalBrakeReleaseMarginKmh = Mathf.Max(0f, settings.normalBrakeReleaseMarginKmh);
            settings.brakeStepChangeIntervalSeconds = Mathf.Max(0.01f, settings.brakeStepChangeIntervalSeconds);
            settings.maximumDownhillGradientPermille = Mathf.Max(0f, settings.maximumDownhillGradientPermille);
            settings.orpSpeedLimitKmh = Mathf.Max(0f, settings.orpSpeedLimitKmh);
            settings.orpMinimumTargetMarginM = Mathf.Max(0f, settings.orpMinimumTargetMarginM);
        }
    }
}
