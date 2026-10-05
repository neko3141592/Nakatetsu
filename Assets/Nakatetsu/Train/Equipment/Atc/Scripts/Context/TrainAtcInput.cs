using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcInput
    {
        // 今回の更新に使う経過時間[s]。
        public float deltaTimeSeconds;

        public bool hasSpeedMeasurement;
        // 編成の固定前方向を正とする測定速度[m/s]。
        public float signedSpeedMps;

        public bool hasCabState;
        public TrainAtcCabInput cab;

        public bool hasDoorState;
        // TIMSで確認した編成全体のドア全閉状態。
        public bool areAllDoorsClosed;

        // 両端の受信結果。未受信側はnullとし、前回の電文を残さない。
        public TrackCircuitAtcTelegram frontTelegram;
        public TrackCircuitAtcTelegram rearTelegram;

        public bool hasCarMasses;
        public readonly List<TrainAtcCarInput> cars = new();
        // 編成の固定前端から受電器までの取り付け距離[m]。
        public float frontReceiverDistanceFromFrontM;
        public float rearReceiverDistanceFromFrontM;

        public bool hasBrakeSettings;
        public TrainAtcBrakeSettingsInput brakeSettings = new();
    }

    public struct TrainAtcCabInput
    {
        // 有効運転台の号車indexと、編成の前後側。
        public int carIndex;
        public bool isFrontCab;
        public bool isKeyInserted;
        public ReverserPosition reverserPosition;
        // マスコンが非常位置にあるか。
        public bool isEmergencyBrake;
    }

    public struct TrainAtcCarInput
    {
        // 空車質量と、応荷重装置で取得した積載質量を合わせた総質量[kg]。
        public float massKg;
        // 編成の固定前端から車両中心までの距離[m]。
        public float centerDistanceFromFrontM;
    }

    public sealed class TrainAtcBrakeSettingsInput
    {
        // 通常ノッチ1段あたりの刻み数。
        public int brakeSubstepCount;
        // 常用最大の刻み段。0は緩解。
        public int maximumServiceBrakeStep;
        // B1から常用最大まで、通常ノッチごとの設定減速度[m/s²]。
        public readonly List<float> brakeTargetDecelerationsMps2 = new();
    }
}
