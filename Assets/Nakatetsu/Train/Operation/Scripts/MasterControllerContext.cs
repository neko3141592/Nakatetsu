using System;

namespace Nakatetsu.Train.Operation
{
    public enum ReverserPosition
    {
        Reverse = -1,
        Neutral = 0,
        Forward = 1
    }

    [Serializable]
    public sealed class MasterControllerState
    {
        public int powerPosition;
        public int brakePosition;
        public ReverserPosition reverserPosition = ReverserPosition.Neutral;
        public bool isInputEnabled = true;
    }

    public sealed class MasterControllerSettings
    {
        public int maxPowerPosition = 4;
        public int maxServiceBrakePosition = 7;
        public int emergencyBrakePosition = 8;
    }

    public sealed class MasterControllerContext
    {
        public MasterControllerSettings Settings { get; } = new();
        public MasterControllerState State { get; } = new();
    }
}
