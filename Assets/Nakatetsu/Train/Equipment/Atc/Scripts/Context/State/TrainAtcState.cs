namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcState
    {
        // 各工程の結果からTrainAtcLogicが決定する、ATC全体の正常性。
        public bool isAtcHealthy;

        public TrainAtcPositionState position = new();
        public TrainAtcOperationState operation = new();
        public TrainAtcValidationState validation = new();
        public TrainAtcProtectionModeState protectionMode = new();
        public TrainAtcPatternState pattern = new();
        public TrainAtcBrakeState brake = new();
    }
}
