using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{

    public sealed class TimsCabInput
    {
        public int powerNotch;
        public int brakeNotch;
    }

    public sealed class TimsNotchInput
    {
        // Set after the caller has resolved the consist and decoded all available cab inputs.
        public bool isReady;

        public readonly List<TimsCabInput> carInputs = new();

        // 運転台
        public CabActivationPosition cabActivationPosition;

        // ATC
        public int atcBrakeStep = 0;
        public bool isAtcEmergency = false;

        // ATO/TASC
        public int tascBrakeStep = 0;
        public int atoPowerNotch = 0;
    }

    public sealed class TimsNotchSettings
    {
        public int powerNotchCount = 1;
        public int brakeNotchCount = 1;
        public int masterControllerEmergencyBrakeNotchPosition = 2;
        public int brakeSubstepCount = 1;
    }

    public sealed class TimsNotchOutput
    {
        public bool isEmergencyBrakeRequested = true;

        public int resolvedPowerNotch;
        public int resolvedBrakeStep;
        public string manualBrakeStepLabel = "B0-0";
        public string brakeStepLabel = "B0-0";
        public string resolvedNotchLabel = "N";
    }

    public sealed class TimsNotchContext
    {
        public TimsNotchInput Input { get; } = new();
        public TimsNotchSettings Settings { get; } = new();
        public TimsNotchOutput Output { get; } = new();
    }
}
