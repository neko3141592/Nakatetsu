using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    [Serializable]
    public sealed class TrainAtcSettings
    {
        // 位置初期化時にATC Graphから取得する。車上設定Assetには保存しない。
        [NonSerialized] public float maximumOperatingSpeedKmh;

        public float MaximumOperatingSpeedMps => maximumOperatingSpeedKmh / 3.6f;

        [Header("Signal")]
        // 前回の有効なパターンを保持できる、連続した無信号時間[s]。0は即時非常。
        [Min(0f)] public float noSignalTimeoutSeconds = 1f;

        [Header("Sampling")]
        [Min(0.01f)] public float maximumSamplingIntervalM = 5f;

        [Header("Pattern Approach")]
        // パターン速度で走った場合の、減速開始前の予告時間[s]。
        [Min(0f)] public float patternApproachWarningTimeSeconds = 5f;

        [Header("Brake")]
        // 暫定の計算用減速度。0以下は未設定として扱う。
        [Min(0f)] public float serviceDecelerationMps2 = 0.5f;
        [Min(0f)] public float emergencyDecelerationMps2 = 1.2f;
        [Min(0f)] public float serviceStopMarginM = 100f;
        [Min(0f)] public float emergencyStopMarginM = 5f;

        [Header("Brake Step")]
        // 常用要求を解除する、パターン速度からの低下幅[km/h]。
        [Min(0f)] public float normalBrakeReleaseMarginKmh = 3f;
        // テーブルによる増減を、刻み1段ずつ行う間隔[s]。
        [Min(0.01f)] public float brakeStepChangeIntervalSeconds = 0.1f;
        // 基準段からの増減数の小さい順に並べる。連番でなくてもよい。速度偏差の単位は[km/h]。
        public List<TrainAtcBrakeStepCondition> brakeStepTable = new()
        {
            new() { brakeStepOffset = -2, increaseToDeviationKmh = -1f, decreaseToDeviationKmh = -1f },
            new() { brakeStepOffset = -1, increaseToDeviationKmh = -0.5f, decreaseToDeviationKmh = -0.5f },
            new() { brakeStepOffset = 0, increaseToDeviationKmh = 0f, decreaseToDeviationKmh = 0f },
            new() { brakeStepOffset = 1, increaseToDeviationKmh = 0.5f, decreaseToDeviationKmh = 0.5f },
            new() { brakeStepOffset = 2, increaseToDeviationKmh = 1f, decreaseToDeviationKmh = 1f }
        };

        [Header("Gradient")]
        // 経路外の非常勾配補正に使う、線区最大の下り勾配の大きさ[‰]。
        [Min(0f)] public float maximumDownhillGradientPermille;

        [Header("ORP")]
        // ORP専用の計算用減速度[m/s²]。0以下は未設定として扱う。
        [Min(0f)] public float orpDecelerationMps2 = 0.7f;
        [Min(0f)] public float orpSpeedLimitKmh = 25f;
        // 停止限界より、この距離以上手前で制限速度へ到達させる。
        [Min(0f)] public float orpMinimumTargetMarginM = 100f;

        public void CopyFrom(TrainAtcSettings source)
        {
            if (source == null) return;

            // 線区最高速度はGraphから取得した値を維持する。
            noSignalTimeoutSeconds = source.noSignalTimeoutSeconds;
            maximumSamplingIntervalM = source.maximumSamplingIntervalM;
            patternApproachWarningTimeSeconds = source.patternApproachWarningTimeSeconds;
            serviceDecelerationMps2 = source.serviceDecelerationMps2;
            emergencyDecelerationMps2 = source.emergencyDecelerationMps2;
            serviceStopMarginM = source.serviceStopMarginM;
            emergencyStopMarginM = source.emergencyStopMarginM;
            normalBrakeReleaseMarginKmh = source.normalBrakeReleaseMarginKmh;
            brakeStepChangeIntervalSeconds = source.brakeStepChangeIntervalSeconds;
            // リストも編成ごとにコピーし、Settings Assetと共有しない。
            if (source.brakeStepTable == null)
            {
                brakeStepTable = new();
            }
            else
            {
                brakeStepTable = new(source.brakeStepTable);
            }
            maximumDownhillGradientPermille = source.maximumDownhillGradientPermille;
            orpDecelerationMps2 = source.orpDecelerationMps2;
            orpSpeedLimitKmh = source.orpSpeedLimitKmh;
            orpMinimumTargetMarginM = source.orpMinimumTargetMarginM;
        }
    }

    [Serializable]
    public struct TrainAtcBrakeStepCondition
    {
        // 基準ノッチからの刻み増減数。0は基準、正は増段、負は減段。
        public int brakeStepOffset;
        // この段へ強めるとき、速度偏差がこの値以上なら切り替える[km/h]。
        public float increaseToDeviationKmh;
        // この段へ弱めるとき、速度偏差がこの値以下なら切り替える[km/h]。
        public float decreaseToDeviationKmh;
    }

    public sealed class TrainAtcInput
    {
        public bool hasCabState;
        public TrainAtcCabInput cab;

        public bool hasBrakeSettings;
        public TrainAtcBrakeSettingsInput brakeSettings = new();

        public bool hasSpeedMeasurement;
        // 編成の固定前方向が正。
        public float signedSpeedMps;
        public float deltaTimeSeconds;

        public bool hasCarMasses;
        public readonly List<TrainAtcCarInput> cars = new();
        // 編成の固定前端から受信機までの取り付け距離[m]。
        public float frontReceiverDistanceFromFrontM;
        public float rearReceiverDistanceFromFrontM;

        // 編成の固定前側（Tc1）と固定後側（Tc2）の受信結果。
        public TrackCircuitAtcTelegram frontTelegram;
        public TrackCircuitAtcTelegram rearTelegram;
    }

    public struct TrainAtcCabInput
    {
        // 有効運転台の号車index。前側は0、後側は編成の最後尾。
        public int carIndex;
        public bool isFrontCab;
        public bool isKeyInserted;
        public bool isInputEnabled;
        public int powerPosition;
        public int brakePosition;
        public int serviceBrakePosition;
        public ReverserPosition reverserPosition;
        public bool isNeutral;
        public bool isEmergencyBrake;
    }

    public struct TrainAtcCarInput
    {
        // 空車質量と応荷重装置の測定した積載分を含む総質量[kg]。
        public float massKg;
        // 編成の固定前端から車両中心までの距離[m]。
        public float centerDistanceFromFrontM;
    }

    public sealed class TrainAtcBrakeSettingsInput
    {
        public int brakeSubstepCount;
        // 常用最大の刻み段。0は緩解。
        public int maximumServiceBrakeStep;
        // B1から常用最大まで、通常ノッチごとの設定減速度[m/s²]。
        public readonly List<float> brakeTargetDecelerationsMps2 = new();
    }

    public interface ITrainAtcCabInputSource
    {
        bool TryReadCabInput(out TrainAtcCabInput input);
    }

    public interface ITrainAtcMassInputSource
    {
        bool TryReadCarInputs(List<TrainAtcCarInput> cars);
    }

    public interface ITrainAtcBrakeSettingsInputSource
    {
        bool TryReadBrakeSettings(TrainAtcBrakeSettingsInput input);
    }

    public sealed class TrainAtcState
    {
        public bool hasCabState;
        public TrainAtcCabInput cab;

        // 有効運転台側の受信電文。未受信または運転台状態が不明ならnull。
        public TrackCircuitAtcTelegram currentTelegram;
        // 現在位置に対応する有効な電文を取得できない間の経過時間[s]。
        public float noSignalElapsedSeconds;

        public bool isPositionInitialized;
        // 位置更新に失敗したら、明示的に補正するまでfalseを保持する。
        public bool isPositionKnown;
        public TrainAtcPosition frontPosition;
        public TrainAtcPosition rearPosition;
        public bool hasCurrentPosition;
        public TrainAtcPosition currentPosition;
        // 有効運転台のレバーサが指定する現在Edge上の進行方向。中立・位置未選択はUnspecified。
        public TrackAtcTravelDirection currentTravelDirection;

        public bool isHealthy;
        public bool isAtcPowerOn;
        public bool isAtcEnabled;

        // 今回の更新前判定。毎tick置き換え、生成・保持・消去を決める。
        public TrainAtcBrakePatternUpdateDecision brakePatternUpdateDecision;
        public TrainAtcBrakePattern brakePattern = new();
        public TrainAtcBrakeState brake = new();
    }

    public struct TrainAtcPosition
    {
        public string atcEdgeId;
        // ATC EdgeのNode Aからの距離[m]。
        public float distanceOnAtcEdgeM;
        // 編成の固定前側（Tc1）がATC EdgeのA→Bを向いているか。
        public bool frontFacesAtoB;
    }

    public struct TrainAtcPathPointInformation
    {
        public string atcEdgeId;
        public string trackCircuitId;
        public float distanceOnPathM;
        // ATC EdgeのNode Aからの距離[m]。
        public float distanceOnAtcEdgeM;
        public TrackAtcTravelDirection direction;
        // 進行方向に対して上りが正[‰]。
        public float gradientPermille;
        // 線区最高速度と常設速度制限の低い方[m/s]。
        public float speedLimitMps;
    }

    public enum TrainAtcBrakePatternUpdateMode
    {
        Clear,
        Create,
        Retain
    }

    public struct TrainAtcBrakePatternUpdateDecision
    {
        public TrainAtcBrakePatternUpdateMode mode;
        public TrackCircuitAtcRouteInfomation routeInfomation;
        public double totalMassKg;
        public float receiverDistanceFromFrontM;
        // 保持する場合は、判定時に求めた現在位置をそのまま使う。
        public float distanceOnPathM;
        public int sampleIndex;
        public float ratio;
    }

    public class TrainAtcBrakePattern
    {
        // 電文を採用した時点の経路の向きと運転台。Edgeを越えても原点を変えない。
        public TrackAtcTravelDirection pathStartTravelDirection;
        public bool isFrontCab;
        public ReverserPosition reverserPosition;
        public OverrunProtectionMode overrunProtectionMode;
        public float samplingIntervalM;
        public float pathLengthM;
        // 今回の現在位置で、パターンを使用できるか。
        public bool hasValidPattern;
        public float distanceOnPathM;
        
        // 現在位置での常用・非常・ORPの許容速度[m/s]。
        public float normalAllowSpeedMps;
        public float emergencyAllowSpeedMps;
        public float orpAllowSpeedMps;
        public float normalTargetSpeedMps;
        public float emergencyPatternTargetMps;
        public float orpPatternTargetMps;
        // 現在位置が、進行方向に見てパターン速度が下がる区間か。
        public bool isNormalDecelerationSection;
        public bool isEmergencyDecelerationSection;
        // 現在位置が、予告区間または減速区間に含まれるか。
        public bool isNormalPatternApproachSection;
        public bool isEmergencyPatternApproachSection;
        public bool isOrpPatternApproachSection;
        public List<string> pathAtcEdges = new();
        public List<TrainAtcBrakePatternSample> normalPattern = new();
        public List<TrainAtcBrakePatternSample> emergencyPattern = new();
        public List<TrainAtcBrakePatternSample> orpPattern = new();
    }

    public struct TrainAtcBrakePatternSample
    {
        // この地点でのパターン上限速度[m/s]。
        public float speedLimitMps;
        // 減速先の目標速度[m/s]。予告区間でも減速先の速度を保持する。
        public float targetSpeedMps;
        // 勾配補正前の計算用減速度[m/s²]。
        public float decelerationMps2;
        // 進行方向に見て、パターン速度が下がる区間。
        public bool isDecelerationSection;
        // 減速区間と、設定時間分だけ手前の予告区間。
        public bool isPatternApproachSection;
    }

    public sealed class TrainAtcBrakeState
    {
        // このtickで非常作動条件を満たしているか。
        public bool isEmergencyRequired;
        // 解除条件を満たすまで非常要求を保持する。
        public bool isEmergencyHold;
        // 緩解条件を満たすまで常用の介入状態を保持する。
        public bool isNormalRequired;

        // 0は緩解。TIMSの刻み段で保持する。
        public int targetBrakeStep;
        public int currentBrakeStep;
        // ヒステリシスで保持するテーブルの行。-1は未選択。
        public int targetBrakeStepTableIndex = -1;

        public float brakeChangeElapsedSeconds;
    }

    public sealed class TrainAtcOutput
    {
        public bool hasValidPattern;
        public bool isOrpActive;
        public bool isPatternApproaching;
        public TrainAtcSignal signal;
        public float distanceOnPathM;
        public float normalAllowSpeedMps;
        public float emergencyAllowSpeedMps;
        public float orpAllowSpeedMps;

        public TrainAtcBrakeOutput brake = new();
    }

    public sealed class TrainAtcBrakeOutput
    {
        public bool isEmergency;
        // 0は緩解。TIMSの刻み段で指定する。
        public int brakeStep;
    }

    public enum TrainAtcSignal
    {
        None = 0,
        Red = 1,
        Green = 2
    }

    public sealed class TrainAtcContext
    {
        public TrainAtcSettings Settings { get; } = new();
        // 車上で使う路線データ。地上ATCの占有・連動状態は参照しない。
        public TrackAtcGraphDefinition Graph { get; internal set; }
        // 初期化時に作成するATC Edge ID -> Edgeの辞書。
        public readonly Dictionary<string, TrackAtcGraphEdge> atcEdgesById = new();
        public TrainAtcInput Input { get; } = new();
        public TrainAtcState State { get; } = new();
        public TrainAtcOutput Output { get; } = new();
    }
}
