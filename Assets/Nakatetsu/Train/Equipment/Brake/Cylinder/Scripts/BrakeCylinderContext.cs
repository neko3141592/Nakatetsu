using System;

namespace Nakatetsu.Train.Equipment.Brake.Cylinder
{
    public sealed class BrakeCylinderInput
    {
        public float targetPressureKPa;
        public float deltaTimeSeconds;
    }

    [Serializable]
    public sealed class BrakeCylinderState
    {
        public float currentPressureKPa;
        public bool isHealthy = true;
    }

    public sealed class BrakeCylinderSettings
    {
        public float maximumPressureKPa = 500f;
        public float applyRateKPaPerSecond = 250f;
        public float releaseRateKPaPerSecond = 350f;
        public float pistonAreaM2 = 0.01f;
        public float mechanicalEfficiency = 0.9f;
    }

    public sealed class BrakeCylinderOutput
    {
        public float actualForceN;
    }

    public sealed class BrakeCylinderContext
    {
        public BrakeCylinderInput Input { get; } = new();
        public BrakeCylinderSettings Settings { get; } = new();
        public BrakeCylinderState State { get; } = new();
        public BrakeCylinderOutput Output { get; } = new();
    }
}
