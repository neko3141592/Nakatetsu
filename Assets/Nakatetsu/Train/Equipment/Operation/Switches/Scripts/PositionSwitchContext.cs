using System;

namespace Nakatetsu.Train.Equipment.Operation.Switches
{
    [Serializable]
    public sealed class PositionSwitchState
    {
        public int position;
    }

    public sealed class PositionSwitchSettings
    {
        public int minimumPosition;
        public int maximumPosition = 1;
        public bool hasSpringReturn;
        public int springReturnPosition;
    }

    public sealed class PositionSwitchContext
    {
        public PositionSwitchSettings Settings { get; } = new PositionSwitchSettings();
        public PositionSwitchState State { get; } = new PositionSwitchState();
    }
}
