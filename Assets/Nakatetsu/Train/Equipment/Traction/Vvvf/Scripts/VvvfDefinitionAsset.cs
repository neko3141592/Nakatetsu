using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Vvvf
{
    [CreateAssetMenu(
        fileName = "VvvfDefinition",
        menuName = "Nakatetsu/Train/Traction/VVVF Definition")]
    public sealed class VvvfDefinitionAsset : ScriptableObject
    {
        [SerializeField] private VvvfSettings settings = new();

        public VvvfSettings Settings => settings;

        public void ApplyTo(VvvfSettings destination) =>
            destination?.CopyFrom(settings);

        private void OnValidate()
        {
            settings ??= new VvvfSettings();
            settings.launchFrequencyHz = Mathf.Max(0f, settings.launchFrequencyHz);
            settings.launchVoltageBoostRatio = Mathf.Clamp(settings.launchVoltageBoostRatio, 0f, 0.5f);
            settings.slipFrequencyControlRateHzPerSecond =
                Mathf.Max(0f, settings.slipFrequencyControlRateHzPerSecond);
            settings.voltageControlRateRatioPerSecond =
                Mathf.Max(0f, settings.voltageControlRateRatioPerSecond);
            settings.torqueDeadbandNm = Mathf.Max(0f, settings.torqueDeadbandNm);
            settings.maximumSlipFrequencyHz = Mathf.Max(0f, settings.maximumSlipFrequencyHz);
            settings.maximumSlipRatio = Mathf.Clamp(settings.maximumSlipRatio, 0f, 0.95f);
            settings.launchSlipFrequencyHz = Mathf.Max(0f, settings.launchSlipFrequencyHz);
            settings.regenTorqueMultiplier = Mathf.Max(0f, settings.regenTorqueMultiplier);
            settings.regenPowerMultiplier = Mathf.Max(0f, settings.regenPowerMultiplier);
            settings.regenCutOutSpeedMps = Mathf.Max(0f, settings.regenCutOutSpeedMps);
            settings.fullRegenSpeedMps = Mathf.Max(
                settings.regenCutOutSpeedMps,
                settings.fullRegenSpeedMps);
            settings.minimumRegenPowerSpeedMps =
                Mathf.Max(0.01f, settings.minimumRegenPowerSpeedMps);
        }
    }
}
