namespace Nakatetsu.Train.Equipment.Atc
{
    public sealed class TrainAtcBrakeState
    {
        public bool isNormalBrakeRequired;
        public bool isEmergencyBrakeRequired;
        public bool isEmergencyHold;
        public bool isRollingPreventing;

        public int targetBrakeStep;
        public int currentBrakeStep;

        // テーブルの行が未選択の場合は-1。
        public int brakeStepTableIndex = -1;
        public float brakeChangeElapsedSeconds;
    }
}
