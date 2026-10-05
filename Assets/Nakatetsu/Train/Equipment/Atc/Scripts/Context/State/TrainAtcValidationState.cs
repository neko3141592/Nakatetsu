using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcValidationState
    {
        // 操作状態・位置・測定速度・計算用入力の基本的な正常性を確認できたか。
        public bool isInputValid;

        // 電文がない、または現在の受電器位置に対応する電文を読めない場合にtrue。
        public bool isNoSignal;
        // 連続した無信号時間[s]。無信号でない場合は常に0へ戻す。
        public float noSignalElapsedSeconds;

        // 今回の情報を採用するか、前回パターンを保持するか、使用不可か。
        public TrainAtcValidationResult result;
        // 新しく採用する候補の経路情報。保持・使用不可の場合はnull。
        public TrackCircuitAtcRouteInfomation routeInfomation;

        // 入力確認時に合計した、編成の総質量[kg]。
        public double totalMassKg;
        // 確認した使用受電器の取り付け位置。編成の固定前端からの距離[m]。
        public float receiverDistanceFromFrontM;
    }

    public enum TrainAtcValidationResult
    {
        // 初期値は使用不可。判定できていない情報を採用しない。
        Unusable = 0,
        // 今回の経路情報を採用し、新しいパターンを生成する。
        Adopt,
        // 無信号の猶予内で、前回の有効なパターンを保持する。
        Retain
    }
}
