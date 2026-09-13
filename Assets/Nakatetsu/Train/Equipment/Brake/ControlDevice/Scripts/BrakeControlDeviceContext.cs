using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Brake.ControlDevice
{
    public sealed class BrakeControlCylinderInput
    {
        public bool isHealthy;
        public float maximumPressureKPa;
        public float pistonAreaM2;
        public float mechanicalEfficiency;
    }

    public sealed class BrakeControlDeviceInput
    {
        public float targetBrakeForceN;
        public readonly List<BrakeControlCylinderInput> cylinders = new();
    }

    public sealed class BrakeControlDeviceOutput
    {
        public bool hasOperationalCylinders;
        public float targetPressureKPa;
        public float maximumBrakeForceN;
    }

    public sealed class BrakeControlDeviceContext
    {
        public BrakeControlDeviceInput Input { get; } = new();
        public BrakeControlDeviceOutput Output { get; } = new();
    }
}
