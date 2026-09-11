using UnityEngine;

namespace Nakatetsu.Train.Traction.Motor
{
    [CreateAssetMenu(
        fileName = "MotorDefinition",
        menuName = "Nakatetsu/Train/Traction/Motor Definition")]
    public sealed class MotorDefinitionAsset : ScriptableObject
    {
        [SerializeField] private MotorSettings settings = new();

        public MotorSettings Settings => settings;

        public void ApplyTo(MotorSettings destination) =>
            destination?.CopyFrom(settings);

        private void OnValidate()
        {
            settings ??= new MotorSettings();
            settings.ratedPowerW = Mathf.Max(0f, settings.ratedPowerW);
            settings.ratedLineVoltageV = Mathf.Max(0f, settings.ratedLineVoltageV);
            settings.ratedCurrentA = Mathf.Max(0f, settings.ratedCurrentA);
            settings.ratedFrequencyHz = Mathf.Max(0.01f, settings.ratedFrequencyHz);
            settings.ratedRpm = Mathf.Max(0.01f, settings.ratedRpm);
            settings.poleCount = Mathf.Max(2, settings.poleCount);
            if ((settings.poleCount & 1) != 0) settings.poleCount++;
            settings.efficiency = Mathf.Clamp01(settings.efficiency);
            settings.powerFactor = Mathf.Clamp01(settings.powerFactor);
            settings.ratedSlipRatio = Mathf.Clamp(settings.ratedSlipRatio, 0f, 0.2f);
            settings.statorResistanceOhm = Mathf.Max(0f, settings.statorResistanceOhm);
            settings.statorReactanceOhm = Mathf.Max(0f, settings.statorReactanceOhm);
            settings.magnetizingReactanceOhm = Mathf.Max(0f, settings.magnetizingReactanceOhm);
            settings.rotorResistanceOhm = Mathf.Max(0f, settings.rotorResistanceOhm);
            settings.rotorReactanceOhm = Mathf.Max(0f, settings.rotorReactanceOhm);
        }
    }
}
