using UnityEngine;
using System;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;

namespace Nakatetsu.Train.Equipment.Tims.Operation
{

    public enum ActivatedCabPosition
    {
        Front = 1,
        None = 0,
        Rear = -1   
    }

    public sealed class TimsDirectionInput
    {
        public int frontCarIndex = 0;
        public int rearCarIndex;

        public bool hasFrontSelection;
        public bool hasRearSelection;
        public CabActivationPosition frontPosition;
        public CabActivationPosition rearPosition;

        public bool hasFrontReverserPosition;
        public bool hasRearReverserPosition;
        public ReverserPosition frontReverserPosition;
        public ReverserPosition rearReverserPosition;
    }

    public sealed class TimsDirectionOutput
    {
        public ActivatedCabPosition activatedCabPosition;
        public ReverserPosition reverserPosition;
        public int consistDirectionSign;
    }
    public class TimsDirectionContext
    {
        public TimsDirectionInput Input { get; } = new();
        public TimsDirectionOutput Output { get; } = new();
    }
}
