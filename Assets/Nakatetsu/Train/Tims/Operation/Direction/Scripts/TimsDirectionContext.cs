using UnityEngine;
using System;
using Nakatetsu.Train.Operation.CabActivationSwitch;

namespace Nakatetsu.Train.Tims.Operation
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
    }

    public sealed class TimsDirectionOutput
    {
        public ActivatedCabPosition activatedCabPosition;
    }
    public class TimsDirectionContext
    {
        public TimsDirectionInput Input { get; } = new();
        public TimsDirectionOutput Output { get; } = new();
    }
}