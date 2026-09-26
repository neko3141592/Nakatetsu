using System;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    public static class EbDeviceLogic
    {
        private const float StopSpeedThresholdMps = 0.01f;

        public static void Calculate(EbDeviceContext context, float deltaTimeSeconds)
        {
            // マスコンの無操作時間を計測してEB要求を更新する。
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            EbDeviceInput input = context.Input;
            EbDeviceState state = context.State;
            EbDeviceOutput output = context.Output;
            float warningDelaySeconds = Math.Max(0f, context.Settings.warningDelaySeconds);
            float activationDelaySeconds = warningDelaySeconds + Math.Max(0f, context.Settings.warningDurationSeconds);
            float speedMps = input.masterController.speedMps;
            bool hasValidInput = input.hasMasterControllerState &&
                !float.IsNaN(speedMps) && !float.IsInfinity(speedMps);

            // 作動後はEB装置自身が保持する。入力欠損や走行中の操作では解除しない。
            if (state.isEmergencyBrakeLatched)
            {
                bool canRelease = hasValidInput && Math.Abs(speedMps) < StopSpeedThresholdMps &&
                    input.masterController.powerPosition == 0;
                if (canRelease)
                {
                    Reset(context, activationDelaySeconds);
                }
                else
                {
                    output.isEmergencyBrakeRequested = true;
                    output.isBuzzerRequested = true;
                    output.inactivitySeconds = state.inactivitySeconds;
                    output.remainingSeconds = 0f;
                }

                return;
            }

            // 前進・後退とも、既定では絶対速度5 km/h以上を走行として監視する。
            bool isRunning = hasValidInput &&
                Math.Abs(speedMps) >= Math.Max(0f, context.Settings.activationSpeedMps);

            if (!input.hasMasterControllerState ||
                !input.masterController.isActiveCab || !input.masterController.isInputEnabled ||
                !isRunning)
            {
                Reset(context, activationDelaySeconds);
                return;
            }

            bool wasOperated = state.hasPreviousMasterControllerState &&
                HasStateChanged(state.previousMasterController, input.masterController);
            state.inactivitySeconds = input.resetRequested || wasOperated
                ? 0f
                : state.inactivitySeconds + Math.Max(0f, deltaTimeSeconds);
            state.previousMasterController = input.masterController;
            state.hasPreviousMasterControllerState = true;

            output.inactivitySeconds = state.inactivitySeconds;
            output.isBuzzerRequested = state.inactivitySeconds >= warningDelaySeconds;
            output.remainingSeconds = Math.Max(0f, activationDelaySeconds - state.inactivitySeconds);
            state.isEmergencyBrakeLatched = state.inactivitySeconds >= activationDelaySeconds;
            output.isEmergencyBrakeRequested = state.isEmergencyBrakeLatched;
        }

        private static bool HasStateChanged(
            EbMasterControllerInput previous,
            EbMasterControllerInput current)
        {
            // 前回から変化したマスコン状態があるか判定する。
            return previous.powerPosition != current.powerPosition ||
                previous.brakePosition != current.brakePosition ||
                previous.reverserPosition != current.reverserPosition ||
                previous.isInputEnabled != current.isInputEnabled;
        }

        private static void Reset(EbDeviceContext context, float activationDelaySeconds)
        {
            // 作動前の監視対象外、または作動後の解除条件成立時に初期化する。
            context.State.isEmergencyBrakeLatched = false;
            context.State.hasPreviousMasterControllerState = false;
            context.State.previousMasterController = default;
            context.State.inactivitySeconds = 0f;
            context.Output.isEmergencyBrakeRequested = false;
            context.Output.isBuzzerRequested = false;
            context.Output.inactivitySeconds = 0f;
            context.Output.remainingSeconds = activationDelaySeconds;
        }
    }
}
