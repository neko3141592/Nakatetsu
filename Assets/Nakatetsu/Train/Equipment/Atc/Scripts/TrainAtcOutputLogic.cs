using Nakatetsu.Track.Simulation.Circuit;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcOutputLogic
    {
        internal static void UpdateOutput(TrainAtcContext context)
        {
            UpdatePatternOutput(context);
            UpdateBrakeOutput(context);
        }

        private static void UpdatePatternOutput(TrainAtcContext context)
        {
            var state = context.State;
            var pattern = state.brakePattern;
            var output = context.Output;
            // 使用できない場合は、前回の表示情報を残さない。
            output.hasValidPattern = pattern.hasValidPattern;
            output.isOrpActive = false;
            output.isPatternApproaching = false;
            output.signal = TrainAtcSignal.None;
            output.distanceOnPathM = 0f;
            output.normalAllowSpeedMps = 0f;
            output.emergencyAllowSpeedMps = 0f;

            if (state.isAtcEnabled)
            {
                // 有効でもパターンを取得できなければ、停止現示にする。
                output.signal = TrainAtcSignal.Red;
            }
            if (!pattern.hasValidPattern)
            {
                return;
            }

            output.distanceOnPathM = pattern.distanceOnPathM;
            output.normalAllowSpeedMps = pattern.normalAllowSpeedMps;
            output.emergencyAllowSpeedMps = pattern.emergencyAllowSpeedMps;

           

            // 後退時も速度の大きさを使い、目標速度と同じ場合も予告する。
            output.isPatternApproaching = pattern.isNormalPatternApproachSection &&
                Mathf.Abs(context.Input.signedSpeedMps) >= pattern.normalTargetSpeedMps;
            // ORP表示は、非常パターンの停止を目標とする予告・降下区間内だけ有効にする。
            output.isOrpActive = pattern.overrunProtectionMode == OverrunProtectionMode.Restricted &&
                pattern.emergencyPatternTargetMps == 0f;

             // 常用パターン速度か減速先の目標速度が0なら、停止現示にする。
            if (pattern.normalAllowSpeedMps == 0f || pattern.normalTargetSpeedMps == 0f || output.isOrpActive)
            {
                output.signal = TrainAtcSignal.Red;
            }
            else
            {
                output.signal = TrainAtcSignal.Green;
            }
        }

        private static void UpdateBrakeOutput(TrainAtcContext context)
        {
            var brake = context.State.brake;
            var output = context.Output.brake;
            // 非常保持中は非常要求を優先し、それ以外は今回の常用段を出力する。
            output.isEmergency = brake.isEmergencyHold;
            if (output.isEmergency)
            {
                output.brakeStep = 0;
            }
            else
            {
                output.brakeStep = brake.currentBrakeStep;
            }
        }
    }
}
