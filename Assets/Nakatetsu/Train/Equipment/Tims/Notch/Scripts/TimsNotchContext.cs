using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{
    public enum TimsCabSelection { Forward = 0, Reverse = 1 }
    public enum TimsReverserPosition { Reverse = -1, Neutral = 0, Forward = 1 }

    public struct TimsCabInput
    {
        public bool hasSelection;
        public TimsCabSelection selection;
        public int powerNotch;
        public int brakeNotch;
        public TimsReverserPosition reverserPosition;
    }

    public sealed class TimsNotchInput
    {
        // Set after the caller has resolved the consist and decoded all available cab inputs.
        public bool isReady;
        public readonly List<TimsCabInput> cars = new();
        public int atcBrakeStep;
    }

    public sealed class TimsNotchSettings
    {
        public int brakeSubstepCount = 1;
    }

    public sealed class TimsNotchOutput
    {
        public int activatedCabIndex = -1;
        public bool isEmergencyBrakeRequested = true;
        public int manualPowerNotch;
        public int manualBrakeStep;
        public int atcBrakeStep;
        public int resolvedPowerNotch;
        public int resolvedBrakeStep;
        public int consistForceSign;
        public TimsReverserPosition reverserPosition;
        public string manualBrakeStepLabel = "B0-0";
        public string atcBrakeStepLabel = "B0-0";
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
