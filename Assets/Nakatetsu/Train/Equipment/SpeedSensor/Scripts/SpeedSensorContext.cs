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
        // 編成の固定前方向が正。ATCの位置更新ではレバーサによらず測定方向を使う。
        public float signedMeasuredSpeedMps;
    }

    public sealed class SpeedSensorContext
    {
        public SpeedSensorInput Input { get; } = new();
        public SpeedSensorOutput Output { get; } = new();
    }
}
