using System;
using Nakatetsu.Train.Equipment.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcBrakeLogic
    {
        private const float StoppedSpeedKmh = 0.1f;

        internal static void UpdateBrakeState(TrainAtcContext context, int baseBrakeStep)
        {
            var state = context.State;
            var brake = state.brake;

            // 取得失敗による初期値を、確認済みのキー切・中立として扱わない。
            if (state.hasCabState)
            {
                if (!state.cab.isKeyInserted || state.cab.reverserPosition == ReverserPosition.Neutral)
                {
                    ClearBrakeState(context);
                    return;
                }
            }

            brake.isEmergencyRequired = HasEmergencyBrakeCause(context);

            if (brake.isEmergencyRequired)
            {
                brake.isEmergencyHold = true;
            }
            else if (brake.isEmergencyHold)
            {
                // 作動原因の解消・停止・マスコン非常位置をすべて確認した場合だけ解除する。
                if (Mathf.Abs(context.Input.signedSpeedMps) <= StoppedSpeedKmh / 3.6f &&
                    state.cab.isEmergencyBrake)
                {
                    brake.isEmergencyHold = false;
                }
            }

            CreateBrakeStep(context, baseBrakeStep);
        }

        private static void CreateBrakeStep(TrainAtcContext context, int baseBrakeStep)
        {
            var brake = context.State.brake;

            if (!context.State.brakePattern.hasValidPattern)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            float speedKmh = Mathf.Abs(context.Input.signedSpeedMps) * 3.6f;
            float normalAllowSpeedKmh = context.State.brakePattern.normalAllowSpeedMps * 3.6f;

            // 停車中かつ常用パターンが5km/h未満の場合は、常用最大
            if (speedKmh <= StoppedSpeedKmh && normalAllowSpeedKmh < 5f)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            // 常用ブレーキが必要か判定
            if (speedKmh > normalAllowSpeedKmh)
            {
                brake.isNormalRequired = true;
            }
            if (speedKmh <= normalAllowSpeedKmh - context.Settings.normalBrakeReleaseMarginKmh)
            {
                brake.isNormalRequired = false;
            }
            if (!brake.isNormalRequired)
            {
                brake.currentBrakeStep = 0;
                brake.targetBrakeStep = 0;
                brake.targetBrakeStepTableIndex = -1;
                brake.brakeChangeElapsedSeconds = 0f;
                return;
            }

            // 以下、ブレーキが必要な場合の段数計算
            if (context.State.brakePattern.isNormalDecelerationSection)
            {
                var table = context.Settings.brakeStepTable;
                var brakeSettings = context.Input.brakeSettings;
                int targetIndex = brake.targetBrakeStepTableIndex;

                // 入力や設定が不正な場合は、常用最大へ即時に切り替える。
                if (table == null || table.Count == 0)
                {
                    SetMaximumServiceBrakeStep(context);
                    return;
                }

                if (baseBrakeStep < 0)
                {
                    SetMaximumServiceBrakeStep(context);
                    return;
                }

                // 基準の行と、段数・閾値の並びを確認する。
                int baseIndex = -1;
                for (int i = 0; i < table.Count; i++)
                {
                    var row = table[i];
                    if (float.IsNaN(row.increaseToDeviationKmh) || float.IsInfinity(row.increaseToDeviationKmh) ||
                        float.IsNaN(row.decreaseToDeviationKmh) || float.IsInfinity(row.decreaseToDeviationKmh))
                    {
                        SetMaximumServiceBrakeStep(context);
                        return;
                    }

                    if (i > 0)
                    {
                        // 前後の値が昇順になっているか確認
                        var previous = table[i - 1];
                        if (row.brakeStepOffset <= previous.brakeStepOffset ||
                            row.increaseToDeviationKmh < previous.increaseToDeviationKmh ||
                            row.decreaseToDeviationKmh < previous.decreaseToDeviationKmh ||
                            previous.decreaseToDeviationKmh >= row.increaseToDeviationKmh)
                        {
                            SetMaximumServiceBrakeStep(context);
                            return;
                        }
                    }

                    if (row.brakeStepOffset == 0)
                    {
                        baseIndex = i;
                    }
                }

                if (baseIndex < 0)
                {
                    SetMaximumServiceBrakeStep(context);
                    return;
                }

                // 介入開始時は基準段から判定し、その後は前回の行を維持する。
                if (targetIndex < 0 || targetIndex >= table.Count)
                {
                    targetIndex = baseIndex;
                    brake.currentBrakeStep = baseBrakeStep;
                    brake.brakeChangeElapsedSeconds = 0f;
                }

                int previousIndex = targetIndex;
                float speedErrorKmh = speedKmh - normalAllowSpeedKmh;

                // 条件を満たす間、次の段へ進む。急な偏差変化でも必要な行まで進める。
                // 上がる条件を照査
                while (targetIndex + 1 < table.Count)
                {
                    if (speedErrorKmh < table[targetIndex + 1].increaseToDeviationKmh)
                    {
                        break;
                    }
                    targetIndex++;
                }

                // 上がる条件に引っかからなかったとき、下がる条件を照査
                if (targetIndex == previousIndex)
                {
                    while (targetIndex > 0)
                    {
                        if (speedErrorKmh > table[targetIndex - 1].decreaseToDeviationKmh)
                        {
                            break;
                        }
                        targetIndex--;
                    }
                }

                // 行は保持したまま、実際の目標段を緩解から常用最大の範囲へ収める。
                brake.targetBrakeStepTableIndex = targetIndex;
                long targetBrakeStep = (long)baseBrakeStep + table[targetIndex].brakeStepOffset;
                brake.targetBrakeStep = (int)Math.Max(0L,
                    Math.Min(brakeSettings.maximumServiceBrakeStep, targetBrakeStep));

                UpdateCurrentBrakeStep(context);
            }
            else
            {
                SetMaximumServiceBrakeStep(context);
            }
        }

        private static void SetMaximumServiceBrakeStep(TrainAtcContext context)
        {
            var brake = context.State.brake;
            // 入力された常用最大段へ即時に切り替え、テーブルの履歴と待ち時間を消す。
            brake.isNormalRequired = true;
            brake.targetBrakeStep = context.Input.brakeSettings.maximumServiceBrakeStep;
            brake.currentBrakeStep = brake.targetBrakeStep;
            brake.targetBrakeStepTableIndex = -1;
            brake.brakeChangeElapsedSeconds = 0f;
        }

        private static void UpdateCurrentBrakeStep(TrainAtcContext context)
        {
            var brake = context.State.brake;
            float intervalSeconds = context.Settings.brakeStepChangeIntervalSeconds;
            float deltaTimeSeconds = context.Input.deltaTimeSeconds;
            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds <= 0f ||
                float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds) || deltaTimeSeconds < 0f)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            if (brake.currentBrakeStep == brake.targetBrakeStep)
            {
                brake.brakeChangeElapsedSeconds = 0f;
                return;
            }

            // シミュレーション時間で計測し、間隔を経過するごとに1段だけ目標へ近づける。
            brake.brakeChangeElapsedSeconds += deltaTimeSeconds;

            while (
                brake.brakeChangeElapsedSeconds >= intervalSeconds &&
                brake.currentBrakeStep != brake.targetBrakeStep
            )
            {
                brake.brakeChangeElapsedSeconds -= intervalSeconds;
                if (brake.currentBrakeStep < brake.targetBrakeStep)
                {
                    brake.currentBrakeStep++;
                }
                else
                {
                    brake.currentBrakeStep--;
                }
            }

            if (brake.currentBrakeStep == brake.targetBrakeStep)
            {
                brake.brakeChangeElapsedSeconds = 0f;
            }
        }

        private static bool HasEmergencyBrakeCause(TrainAtcContext context)
        {
            bool isEmergencyPatternExceeded = 
                Mathf.Abs(context.Input.signedSpeedMps) > context.State.brakePattern.emergencyAllowSpeedMps;
            // 電文・パターンの可否は、今回まとめて判定した結果を使う。
            return !context.State.brakePattern.hasValidPattern || isEmergencyPatternExceeded;
        }

        private static void ClearBrakeState(TrainAtcContext context)
        {
            var brake = context.State.brake;
            brake.isEmergencyRequired = false;
            brake.isEmergencyHold = false;
            brake.isNormalRequired = false;
            brake.targetBrakeStep = 0;
            brake.currentBrakeStep = 0;
            brake.targetBrakeStepTableIndex = -1;
            brake.brakeChangeElapsedSeconds = 0f;
        }
    }
}
