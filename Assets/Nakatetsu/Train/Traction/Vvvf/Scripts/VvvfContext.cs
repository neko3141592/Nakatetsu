using System;
using UnityEngine;

namespace Nakatetsu.Train.Traction.Vvvf
{
    public enum VvvfDriveMode
    {
        Neutral,
        Power,
        Regen
    }

    [Serializable]
    public sealed class VvvfSettings
    {
        [Header("Control")]
        [Min(0f)] public float launchFrequencyHz = 0.5f;
        [Range(0f, 0.5f)] public float launchVoltageBoostRatio = 0.04f;
        [Min(0f)] public float slipFrequencyControlRateHzPerSecond = 0.45f;
        [Min(0f)] public float voltageControlRateRatioPerSecond = 0.45f;
        [Min(0f)] public float torqueDeadbandNm = 5f;
        [Min(0f)] public float maximumSlipFrequencyHz = 1.5f;

        [Header("Slip Limits")]
        [Range(0f, 0.95f)] public float maximumSlipRatio = 0.16f;
        [Min(0f)] public float launchSlipFrequencyHz = 0.2f;

        [Header("Regenerative Brake")]
        [Min(0f)] public float regenTorqueMultiplier = 1.2f;
        [Min(0f)] public float regenPowerMultiplier = 1f;
        [Min(0f)] public float regenCutOutSpeedMps = 0.2f;
        [Min(0f)] public float fullRegenSpeedMps = 1f;
        [Min(0.01f)] public float minimumRegenPowerSpeedMps = 1f;

        public void CopyFrom(VvvfSettings source)
        {
            if (source == null) return;

            launchFrequencyHz = source.launchFrequencyHz;
            launchVoltageBoostRatio = source.launchVoltageBoostRatio;
            slipFrequencyControlRateHzPerSecond = source.slipFrequencyControlRateHzPerSecond;
            voltageControlRateRatioPerSecond = source.voltageControlRateRatioPerSecond;
            torqueDeadbandNm = source.torqueDeadbandNm;
            maximumSlipFrequencyHz = source.maximumSlipFrequencyHz;
            maximumSlipRatio = source.maximumSlipRatio;
            launchSlipFrequencyHz = source.launchSlipFrequencyHz;
            regenTorqueMultiplier = source.regenTorqueMultiplier;
            regenPowerMultiplier = source.regenPowerMultiplier;
            regenCutOutSpeedMps = source.regenCutOutSpeedMps;
            fullRegenSpeedMps = source.fullRegenSpeedMps;
            minimumRegenPowerSpeedMps = source.minimumRegenPowerSpeedMps;
        }
    }

    [Serializable]
    public sealed class VvvfState
    {
        public float responseVariation = 1f;
        public float slipFrequencyHz;
        public float voltageRatio;
        public float phaseRad;
    }

    public sealed class VvvfInput
    {
        public float deltaTimeSeconds;
        public float vehicleSpeedMps;
        public float targetTractionForceN;
        public float wheelRadiusM;
        public float gearRatio;
        public float transmissionEfficiency;
        public int motorCount;
        public float representativeMotorTorqueNm;
        public float ratedMotorLineVoltageV;
        public float ratedMotorFrequencyHz;
        public int motorPoleCount;
    }

    public sealed class VvvfOutput
    {
        public VvvfDriveMode driveMode;
        public float targetMotorTorqueNm;
        public float frequencyHz;
        public float synchronousRpm;
        public float slipRatio;
        public float rotorBaseFrequencyHz;
        public float lineVoltageRmsV;
        public float phaseVoltagePeakV;
        public float wheelRpm;
        public float motorRpm;
        public float uPhaseV;
        public float vPhaseV;
        public float wPhaseV;

        public void Reset()
        {
            driveMode = VvvfDriveMode.Neutral;
            targetMotorTorqueNm = 0f;
            frequencyHz = 0f;
            synchronousRpm = 0f;
            slipRatio = 0f;
            rotorBaseFrequencyHz = 0f;
            lineVoltageRmsV = 0f;
            phaseVoltagePeakV = 0f;
            wheelRpm = 0f;
            motorRpm = 0f;
            uPhaseV = 0f;
            vPhaseV = 0f;
            wPhaseV = 0f;
        }
    }

    public sealed class VvvfContext
    {
        public VvvfSettings Settings { get; } = new();
        public VvvfState State { get; } = new();
        public VvvfInput Input { get; } = new();
        public VvvfOutput Output { get; } = new();
    }
}
