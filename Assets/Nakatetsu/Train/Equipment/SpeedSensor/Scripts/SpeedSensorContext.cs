namespace Nakatetsu.Train.Equipment.SpeedMeasurement
{
    public sealed class SpeedSensorInput
    {
        public bool hasPhysicalSpeed;
        public float signedPhysicalSpeedMps;
    }

    public sealed class SpeedSensorOutput
    {
        public bool hasMeasurement;
        public float measuredSpeedMps;
    }

    public sealed class SpeedSensorContext
    {
        public SpeedSensorInput Input { get; } = new();
        public SpeedSensorOutput Output { get; } = new();
    }
}
