using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcOutputLogic
    {
        internal static void UpdateAtcOutput(TrainAtcContext context)
        {
            UpdateAtcStateOutput(context);
            UpdateAtcBrakeOutput(context);
            UpdateAtcPatternOutput(context);
        }

        private static void UpdateAtcStateOutput(TrainAtcContext context)
        {
            var state = context.Output.state;

            state.isAtcPowerOn = context.State.operation.isAtcPowerOn;
            state.isAtcEnabled = context.State.operation.isAtcEnabled;
            state.isAtcHealthy = context.State.isAtcHealthy;
            state.isBrakeReleased = false;
        }

        private static void UpdateAtcBrakeOutput(TrainAtcContext context)
        {
            var brake = context.Output.brake;

            // 非常の原因が消えた後も、解除条件を満たすまでは非常指令を維持する。
            brake.isEmergencyBrakeRequired = context.State.brake.isEmergencyHold;
            brake.isNormalBrakeRequired = context.State.brake.isNormalBrakeRequired;
            brake.isRollingPreventing = context.State.brake.isRollingPreventing;

            if (brake.isEmergencyBrakeRequired)
            {
                brake.brakeStep = 0;
            }
            else
            {
                brake.brakeStep = context.State.brake.currentBrakeStep;
            }
        }

        private static void UpdateAtcPatternOutput(TrainAtcContext context)
        {
            var pattern = context.Output.pattern;

            float allowSpeedMps = Mathf.Min(
                context.State.pattern.normalPattern.currentAllowSpeedMps,
                context.State.pattern.emergencyPattern.currentAllowSpeedMps
            );

            // 過走防護を使用しない場合は、未生成のORPパターンを比較に含めない。
            if (context.State.protectionMode.overrunProtectionMode ==
                Track.Simulation.Circuit.OverrunProtectionMode.Restricted)
            {
                allowSpeedMps = Mathf.Min(
                    allowSpeedMps,
                    context.State.pattern.orpPattern.currentAllowSpeedMps
                );
            }

            // ORP作動判定は、終端保護方式が過走防護かつORPパターンが低下している
            bool isOrpOperating =
                context.State.protectionMode.overrunProtectionMode == Track.Simulation.Circuit.OverrunProtectionMode.Restricted &&
                context.State.pattern.orpPattern.isDecelerationSection;
            pattern.isOrpOperating = isOrpOperating;

            pattern.allowSpeedMps = allowSpeedMps;

            pattern.isSpeedIndicated =
                context.State.isAtcHealthy &&
                context.State.operation.isAtcEnabled;
            pattern.indicatedSpeedKmh = 5f * Mathf.FloorToInt(allowSpeedMps * 3.6f / 5f);

            // 後退時も速度の大きさを使い、目標速度と同じ場合も予告する。
            pattern.isPatternApproaching =
                context.State.pattern.normalPattern.isApproachSection &&
                Mathf.Abs(context.Input.signedSpeedMps) >= context.State.pattern.normalPattern.currentTargetSpeedMps;

            if (!context.State.isAtcHealthy || !context.State.operation.isAtcEnabled)
            {
                pattern.signal = TrainAtcSignal.None;
            }
            else if (context.State.pattern.normalPattern.currentTargetSpeedMps > 0f && !isOrpOperating)
            {
                pattern.signal = TrainAtcSignal.Green;
            }
            else
            {
                pattern.signal = TrainAtcSignal.Red;
            }
        }
    }
}
