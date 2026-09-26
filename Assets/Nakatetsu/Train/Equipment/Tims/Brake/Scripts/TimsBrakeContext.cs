using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    /// <summary>Decoded telemetry and definition values for one car; forces are in N, mass in kg.</summary>
    public sealed class TimsBrakeCarInput
    {
        public float massKg;

        public bool isVvvfMotorCar;
        public bool isTrailerCar;

        // 実ブレーキ力
        public float regenForceN;
        public float airForceN;
        public float bcPressureKPa;

        // 上限
        public float regenCapN;
        public float airCapN;

        public float airForcePerKPa;
        public float maxBCPressureKPa;
    }

    public sealed class TimsBrakeInput
    {
        // そのステップで集約された非常要求。保持状態は持たない。
        public bool isEmergencyBrakeRequested = true;
        public int brakeStep;
        public readonly List<TimsBrakeCarInput> cars = new();
    }

    public sealed class TimsBrakeSettings
    {
        public int brakeSubstepCount = 1;
        public readonly List<float> brakeTargetDecelerationsMps2 = new();
        public float minimumServiceBrakePressureKPa = 40f;
        public float minimumServiceBrakePressureLoadScaleMax = 3f;
    }

    public sealed class TimsBrakeCarCommand
    {
        public float targetRegenForceN;
        public float targetAirForceN;
        public float targetAirPressureKPa;
        // Legacy tag meaning: per-car additional target, excluding minimum air preload.
        public float targetBrakeForceN;
        public bool isEmergency;
    }

    public sealed class TimsBrakeOutput
    {
        public bool isEmergency = true;
        // False means only emergency status is new; do not publish the retained command arrays.
        public bool hasCommands;
        public readonly List<TimsBrakeCarCommand> carCommands = new();
    }

    public sealed class TimsBrakeWorkspace
    {
        public float targetDecelerationMps2;
        public float targetTotalBrakeForceN;
        public float remainingTargetBrakeForceN;

        // 全車共通の最低込め圧。
        public float minimumAirPressureKPa;

        public List<float> targetCarBrakeForcesN = new();
        public List<float> targetRegenForcesN = new();
        public List<float> additionalAirForcesN = new();
        public List<float> minimumAirForcesN = new();
    }

    public sealed class TimsBrakeContext
    {
        public TimsBrakeInput Input { get; } = new();
        public TimsBrakeSettings Settings { get; } = new();
        public TimsBrakeOutput Output { get; } = new();
        public TimsBrakeWorkspace Workspace { get; } = new();
    }
}
