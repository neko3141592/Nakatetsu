namespace Nakatetsu.Train.Equipment.Atc
{
    public sealed class TrainAtcOutput
    {
        public TrainAtcStateOutput state = new();
        public TrainAtcBrakeOutput brake = new();
        public TrainAtcPatternOutput pattern = new();
    }

    public sealed class TrainAtcStateOutput
    {
        // ATC電源。
        public bool isAtcPowerOn;
        // 速度照査が有効か。
        public bool isAtcEnabled;
        // ATC全体が正常か。
        public bool isAtcHealthy;

        // ATCブレーキ開放（後で実装）。
        public bool isBrakeReleased;
    }

    public sealed class TrainAtcBrakeOutput
    {
        // ATC非常ブレーキ要求。
        public bool isEmergencyBrakeRequired;

        // ATC常用ブレーキ要求。
        public bool isNormalBrakeRequired;

        // ドア全閉未確認による転動防止。
        public bool isRollingPreventing;

        // 出力する常用ブレーキ段。0は緩解。非常要求は別に扱う。
        public int brakeStep;
    }

    public sealed class TrainAtcPatternOutput
    {
        // 現在位置の常用パターン許容速度[m/s]。表示用の丸めは行わない。
        public float allowSpeedMps;

        // モニターにスピードを現示するか
        public bool isSpeedIndicated;

        // モニター用の現示速度[km/h]。表示仕様に従った丸めやORP表示を反映する。
        public float indicatedSpeedKmh;

        // パターン接近表示。
        public bool isPatternApproaching;

        // 過走防護表示。
        public bool isOrpOperating;

        // 赤・緑・消灯の表示結果。
        public TrainAtcSignal signal;
    }

    public enum TrainAtcSignal
    {
        None = 0,
        Red = 1,
        Green = 2
    }
}
