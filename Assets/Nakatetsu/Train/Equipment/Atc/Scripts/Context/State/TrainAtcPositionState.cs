namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcPositionState
    {

        // ゲーム開始時に、両端受電器の初期位置と編成の向きを設定済みか。
        public bool isPositionInitialized;

        // 現在の受電器位置を正しく更新できているか。
        public bool isPositionKnown;
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
