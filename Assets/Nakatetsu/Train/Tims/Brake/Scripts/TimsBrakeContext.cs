using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Tims.Brake
{
    /// <summary>Decoded telemetry and definition values for one car; forces are in N, mass in kg.</summary>
    public sealed class TimsBrakeCarInput
    {
        public float massKg;
        public bool isVvvfMotorCar;
        public bool isTrailerCar;
        public float regenForceN;
        public float airForceN;
        public float bcPressureKPa;
        public float regenCapN;
        public float airCapN;
        public float airForcePerKPa;
        public float maxBCPressureKPa;
    }

    public sealed class TimsBrakeInput
    {
        // Replaces the legacy check for TIMS, consist, config and terminal availability.
        public bool canReleaseEmergencyBrake;
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
        // Legacy tag meaning: per-car additional target, excluding minimum air preload.
        public float targetBrakeForceN;
        public bool isEmergency;
    }

    public sealed class TimsBrakeOutput
    {
        public bool isEmergency = true;
        // False means only emergency status is new; do not publish the retained command arrays.
        public bool hasCommands;
        public float totalMassKg;
        public float targetTotalBrakeForceN;
        public float actualRegenTotalForceN;
        public readonly List<TimsBrakeCarCommand> carCommands = new();
    }

    public sealed class TimsBrakeWorkspace
    {
        public readonly List<float> carMassesKg = new();
        public readonly List<float> targetCarBrakeForcesN = new();
        public readonly List<float> targetRegenForcesN = new();
        public readonly List<float> additionalAirForcesN = new();
        public readonly List<float> minimumAirForcesN = new();
    }

    public sealed class TimsBrakeContext
    {
        public TimsBrakeInput Input { get; } = new();
        public TimsBrakeSettings Settings { get; } = new();
        public TimsBrakeOutput Output { get; } = new();
        public TimsBrakeWorkspace Workspace { get; } = new();
    }
}
