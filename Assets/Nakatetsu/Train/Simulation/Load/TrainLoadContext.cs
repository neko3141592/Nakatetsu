namespace Nakatetsu.Train.Simulation.Load
{
    public sealed class TrainLoadInput
    {
        public int passengerCount;
        public float cargoMassKg;
    }

    public sealed class TrainLoadSettings
    {
        public float emptyMassKg;
        public int passengerCapacity;
        public float averagePassengerMassKg = 55f;
    }

    public sealed class TrainLoadState
    {
        public bool isInitialized;
        public int actualPassengerCount;
        public float actualPassengerMassKg;
        public float actualCargoMassKg;
        public float actualTotalMassKg;
        public float actualSupportedMassKg;
    }

    public sealed class TrainLoadOutput
    {
        public int actualPassengerCount;
        public float actualPassengerMassKg;
        public float actualCargoMassKg;
        public float actualTotalMassKg;
        public float actualSupportedMassKg;
    }

    public sealed class TrainLoadContext
    {
        public TrainLoadInput Input { get; } = new();
        public TrainLoadSettings Settings { get; } = new();
        public TrainLoadState State { get; } = new();
        public TrainLoadOutput Output { get; } = new();
    }
}
