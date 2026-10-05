using System;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcBrakeLogic
    {
        private const float StoppedSpeedKmh = 0.1f;
        private const float StopHoldPatternSpeedKmh = 5f;

        internal static void UpdateBrakeState(TrainAtcContext context)
        {
            var operation = context.State.operation;

            // 取得失敗による初期値を、確認済みのキー切・中立として扱わない。
            if (operation.hasCabState)
            {
                if (!operation.cab.isKeyInserted ||
                    operation.cab.reverserPosition == ReverserPosition.Neutral)
                {
                    ClearBrakeState(context);
                    return;
                }
            }

            // 毎tick全閉確認を更新し、前回の転動防止状態を残さない。
            // ドア状態を取得できない間も、全閉未確認として常用最大を要求する。
            context.State.brake.isRollingPreventing =
                !context.Input.hasDoorState || !context.Input.areAllDoorsClosed;

            UpdateEmergencyBrakeState(context);

            // 各工程の確認結果を使い、入力・電文の確認をこの工程で繰り返さない。
            if (!context.State.isAtcHealthy || !context.State.pattern.isValid)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            UpdateNormalBrakeState(context);
        }

        private static void UpdateEmergencyBrakeState(TrainAtcContext context)
        {
            var brake = context.State.brake;
            brake.isEmergencyBrakeRequired = HasEmergencyBrakeCause(context);

            if (brake.isEmergencyBrakeRequired)
            {
                brake.isEmergencyHold = true;
            }
            else if (brake.isEmergencyHold)
            {
                // 作動原因の解消・停止・マスコン非常位置をすべて確認した場合だけ解除する。
                if (Mathf.Abs(context.Input.signedSpeedMps) <= StoppedSpeedKmh / 3.6f &&
                    context.State.operation.cab.isEmergencyBrake)
                {
                    brake.isEmergencyHold = false;
                }
            }
        }

        private static bool HasEmergencyBrakeCause(TrainAtcContext context)
        {
            if (!context.State.isAtcHealthy || !context.State.operation.isAtcEnabled ||
                !context.State.pattern.isValid)
            {
                return true;
            }

            float speedMps = Mathf.Abs(context.Input.signedSpeedMps);
            if (speedMps > context.State.pattern.emergencyPattern.currentAllowSpeedMps)
            {
                return true;
            }

            if (context.State.protectionMode.overrunProtectionMode == OverrunProtectionMode.Restricted)
            {
                if (speedMps > context.State.pattern.orpPattern.currentAllowSpeedMps)
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateNormalBrakeState(TrainAtcContext context)
        {
            var brake = context.State.brake;
            var normalPattern = context.State.pattern.normalPattern;
            float speedKmh = Mathf.Abs(context.Input.signedSpeedMps) * 3.6f;
            float normalAllowSpeedKmh = normalPattern.currentAllowSpeedMps * 3.6f;

            // 全閉確認後は、この分岐を通らず通常の介入・緩解判定へ戻る。
            if (brake.isRollingPreventing)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            // 常用パターンが5km/h未満で停車中の場合は、緩解条件より停止保持を優先する。
            if (speedKmh <= StoppedSpeedKmh && normalAllowSpeedKmh < StopHoldPatternSpeedKmh)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            if (speedKmh > normalAllowSpeedKmh)
            {
                brake.isNormalBrakeRequired = true;
            }
            if (speedKmh <= normalAllowSpeedKmh - context.Settings.normalBrakeReleaseMarginKmh)
            {
                brake.isNormalBrakeRequired = false;
            }

            if (!brake.isNormalBrakeRequired)
            {
                ClearNormalBrakeStep(context);
                return;
            }

            if (!normalPattern.isDecelerationSection)
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            if (!TryGetBaseBrakeStep(context, out int baseBrakeStep))
            {
                SetMaximumServiceBrakeStep(context);
                return;
            }

            UpdateTargetBrakeStep(context, baseBrakeStep, speedKmh - normalAllowSpeedKmh);
            UpdateCurrentBrakeStep(context);
        }

        private static bool TryGetBaseBrakeStep(TrainAtcContext context, out int baseBrakeStep)
        {
            var brakeSettings = context.Input.brakeSettings;
            return TrainAtcBrakeHelper.TryGetNearestBrakeStep(
                context.State.pattern.normalPattern.currentDecelerationMps2,
                brakeSettings.brakeTargetDecelerationsMps2,
                brakeSettings.brakeSubstepCount,
                brakeSettings.maximumServiceBrakeStep,
                out baseBrakeStep);
        }

        private static void UpdateTargetBrakeStep(
            TrainAtcContext context,
            int baseBrakeStep,
            float speedErrorKmh)
        {
            var brake = context.State.brake;
            var table = context.Settings.brakeStepTable;
            int targetIndex = brake.brakeStepTableIndex;

            // 介入開始時は基準段を即時に設定し、以降のテーブルによる増減に待ち時間を設ける。
            if (targetIndex < 0 || targetIndex >= table.Count)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    if (table[i].brakeStepOffset == 0)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                brake.currentBrakeStep = baseBrakeStep;
                brake.brakeChangeElapsedSeconds = 0f;
            }

            int previousIndex = targetIndex;

            // 急な速度偏差の変化では、同じtickで条件を満たす行まで移動する。
            while (targetIndex + 1 < table.Count)
            {
                if (speedErrorKmh < table[targetIndex + 1].increaseToDeviationKmh)
                {
                    break;
                }

                targetIndex++;
            }

            // 増段条件に該当しなかった場合は、一つ下の行の減段条件を使う。
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

            brake.brakeStepTableIndex = targetIndex;
            long targetBrakeStep = (long)baseBrakeStep + table[targetIndex].brakeStepOffset;
            brake.targetBrakeStep = (int)Math.Max(0L,
                Math.Min(context.Input.brakeSettings.maximumServiceBrakeStep, targetBrakeStep));
        }

        private static void UpdateCurrentBrakeStep(TrainAtcContext context)
        {
            var brake = context.State.brake;
            if (brake.currentBrakeStep == brake.targetBrakeStep)
            {
                brake.brakeChangeElapsedSeconds = 0f;
                return;
            }

            double intervalSeconds = context.Settings.brakeStepChangeIntervalSeconds;
            double elapsedSeconds = (double)brake.brakeChangeElapsedSeconds + context.Input.deltaTimeSeconds;

            // 経過した間隔の回数分だけ1stepずつ進める。目標までの段数を上限とする。
            long remainingSteps = Math.Abs((long)brake.targetBrakeStep - brake.currentBrakeStep);
            int changeSteps = (int)Math.Min(remainingSteps, Math.Floor(elapsedSeconds / intervalSeconds));
            if (brake.currentBrakeStep < brake.targetBrakeStep)
            {
                brake.currentBrakeStep += changeSteps;
            }
            else
            {
                brake.currentBrakeStep -= changeSteps;
            }

            brake.brakeChangeElapsedSeconds = (float)(elapsedSeconds - changeSteps * intervalSeconds);
            if (brake.currentBrakeStep == brake.targetBrakeStep)
            {
                brake.brakeChangeElapsedSeconds = 0f;
            }
        }

        private static void SetMaximumServiceBrakeStep(TrainAtcContext context)
        {
            var brake = context.State.brake;
            // 不正入力時にも非常判定を続ける。常用設定がなければ、常用段は0とする。
            int maximumServiceBrakeStep = context.Input.brakeSettings?.maximumServiceBrakeStep ?? 0;
            brake.isNormalBrakeRequired = true;
            brake.targetBrakeStep = Math.Max(0, maximumServiceBrakeStep);
            brake.currentBrakeStep = brake.targetBrakeStep;
            brake.brakeStepTableIndex = -1;
            brake.brakeChangeElapsedSeconds = 0f;
        }

        private static void ClearNormalBrakeStep(TrainAtcContext context)
        {
            var brake = context.State.brake;
            brake.targetBrakeStep = 0;
            brake.currentBrakeStep = 0;
            brake.brakeStepTableIndex = -1;
            brake.brakeChangeElapsedSeconds = 0f;
        }

        private static void ClearBrakeState(TrainAtcContext context)
        {
            var brake = context.State.brake;
            brake.isEmergencyBrakeRequired = false;
            brake.isEmergencyHold = false;
            brake.isNormalBrakeRequired = false;
            brake.isRollingPreventing = false;
            ClearNormalBrakeStep(context);
        }
    }
}
