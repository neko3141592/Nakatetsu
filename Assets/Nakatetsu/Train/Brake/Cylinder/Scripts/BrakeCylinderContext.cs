using System;

namespace Nakatetsu.Train.Brake.Cylinder
{
    [Serializable]
    public sealed class BrakeCylinderState
    {
        public float currentPressureKPa;
        public bool isHealthy = true;
    }

    public sealed class BrakeCylinderContext
    {
        public BrakeCylinderState State { get; } = new();
    }
}
