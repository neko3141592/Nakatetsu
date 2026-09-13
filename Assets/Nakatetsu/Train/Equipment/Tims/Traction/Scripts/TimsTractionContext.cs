using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    public enum TimsSpeedHoldMode { Off, Arming, Active }

    [Serializable]
    public sealed class TimsTractionState
    {
        public TimsSpeedHoldMode speedHoldMode;
        public float speedHoldArmingTimerSeconds;
        public float speedHoldTargetMps;
    }

    public struct TimsTractionUnitInput
    {
        public bool isAvailable;
        public bool hasMotorSettings;
        public int motorCount;
        public float ratedMotorPowerW;
    }

    public sealed class TimsTractionInput
    {
        public bool isReady;
        public float deltaTimeSeconds;
        public float speedMps;
        public float consistMassKg;
        public int brakeStep;
        public int powerNotch;
        public int manualPowerNotch;
        public int manualBrakeNotch;
        public int atcBrakeNotch;
        public bool isGradientStart;
        public float currentBCPressureKPa;
        // Sampled by the caller from the selected notch's AnimationCurve at normalized speed.
        public float powerStepGain;
        public readonly List<float> carBCPressuresKPa = new();
        public readonly List<TimsTractionUnitInput> units = new();
    }

    public sealed class TimsTractionSettings
    {
        public float launchAccelerationMps2 = 3f / 3.6f;
        public float bcReleaseThresholdKPa = 5f;
        public float speedHoldArmingSeconds = 1f;
    }

    public sealed class TimsTractionOutput
    {
        public float targetForceN;
        public float ratedConsistPowerW;
        public float targetAccelerationMps2;
        public float constantAccelerationEndSpeedMps;
        public float targetForcePerVvvfN;
        public int activeVvvfCount;
        public bool isBCReleaseInterlockActive;
        // The legacy brake-active branch does not dispatch a VVVF command.
        public bool hasForceCommand;
        public string regionLabel = "--";
        // Aligned with Input.units; unavailable units receive zero.
        public readonly List<float> unitTargetForcesN = new();
    }

    public sealed class TimsTractionContext
    {
        public TimsTractionState State { get; } = new();
        public TimsTractionInput Input { get; } = new();
        public TimsTractionSettings Settings { get; } = new();
        public TimsTractionOutput Output { get; } = new();
    }
}
