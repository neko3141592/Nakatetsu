using System;
using System.Collections.Generic;
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
        // 基準段からの増減数の小さい順に並べる。連番でなくてもよい。
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
            if (source == null)
            {
                return;
            }

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
}
