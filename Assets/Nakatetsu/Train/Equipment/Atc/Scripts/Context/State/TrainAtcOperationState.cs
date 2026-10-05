using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcOperationState
    {
        // 操作状態を確認できたか。正常なキー切・中立と取得失敗を区別する。
        public bool hasCabState;
        public TrainAtcCabInput cab;

        // ATC電源。キー切・中立・操作状態の取得失敗でも常にtrue。
        public bool isAtcPowerOn = true;

        // 操作状態を確認でき、キー投入かつレバーサが前進・後退の場合に有効。
        public bool isAtcEnabled;

        // 前後側は編成の固定前側・固定後側。未選択はNone。
        public TrainAtcReceiverSide selectedReceiver;

        // 速度照査に使用する受電器の位置を選択できているか。
        public bool hasCurrentPosition;
        // キー切・中立・選択不可の場合はnull。
        public TrainAtcPosition currentPosition;
        // 運転台側・レバーサ・編成の向きから求める照査方向。未選択はUnspecified。
        public TrackAtcTravelDirection currentTravelDirection;

        // 使用受電器側の今回の電文。未受信・未選択はnull。
        public TrackCircuitAtcTelegram currentTelegram;
    }

    public enum TrainAtcReceiverSide
    {
        None = 0,
        Front,
        Rear
    }
}
