namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcPositionState
    {

        // ゲーム開始時に、両端受電器の初期位置と編成の向きを設定済みか。
        public bool isPositionInitialized;

        // 固定前側・固定後側の最新位置を、今回の更新で解決できているか。
        public bool isFrontPositionKnown;
        public bool isRearPositionKnown;
        // 両端既知の診断用集約。照査には選択した側の結果を使う。
        public bool IsPositionKnown => isFrontPositionKnown && isRearPositionKnown;
        public TrainAtcPosition frontPosition;
        public TrainAtcPosition rearPosition;

    }

    public class TrainAtcPosition
    {
        public string atcEdgeId;
        // ATC EdgeのNode Aからの距離[m]。
        public float distanceOnAtcEdgeM;
        // 編成の固定前側（Tc1）がATC EdgeのA→Bを向いているか。
        public bool frontFacesAtoB;
    }
}
