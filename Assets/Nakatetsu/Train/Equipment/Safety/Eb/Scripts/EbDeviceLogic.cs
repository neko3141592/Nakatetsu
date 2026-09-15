using System;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    public static class EbDeviceLogic
    {
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
            float activationDelaySeconds = Math.Max(0f, context.Settings.activationDelaySeconds);
            float speedMps = input.masterController.speedMps;
            // 前進・後退とも、既定では絶対速度5 km/h以上を走行として監視する。
            bool isRunning = !float.IsNaN(speedMps) && !float.IsInfinity(speedMps) &&
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
            state.inactivitySeconds = wasOperated
                ? 0f
                : state.inactivitySeconds + Math.Max(0f, deltaTimeSeconds);
            state.previousMasterController = input.masterController;
            state.hasPreviousMasterControllerState = true;

            output.inactivitySeconds = state.inactivitySeconds;
            output.remainingSeconds = Math.Max(0f, activationDelaySeconds - state.inactivitySeconds);
            output.isEmergencyBrakeRequested = state.inactivitySeconds >= activationDelaySeconds;
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
            // 監視対象外のときにEB状態を初期化する。
            context.State.hasPreviousMasterControllerState = false;
            context.State.previousMasterController = default;
            context.State.inactivitySeconds = 0f;
            context.Output.isEmergencyBrakeRequested = false;
            context.Output.inactivitySeconds = 0f;
            context.Output.remainingSeconds = activationDelaySeconds;
        }
    }
}
