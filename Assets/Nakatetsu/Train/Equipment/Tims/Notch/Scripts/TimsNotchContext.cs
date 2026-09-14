using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{

    public struct TimsCabInput
    {
        public int powerNotch;
        public int brakeNotch;
    }

    public sealed class TimsNotchInput
    {
        // Set after the caller has resolved the consist and decoded all available cab inputs.
        public bool isReady;
        public readonly List<TimsCabInput> cars = new();

        // ATC
        public int atcBrakeStep;

        // ATO/TASC
        public int tascBrakeStep;
        public int atoPowerNotch;
    }

    public sealed class TimsNotchSettings
    {
        public int brakeSubstepCount = 1;
    }

    public sealed class TimsNotchOutput
    {
        public bool isEmergencyBrakeRequested = true;
        public int manualPowerNotch;
        public int manualBrakeStep;
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
