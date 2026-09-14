using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{
    public static class TimsNotchLogic
    {
        public static void Calculate(TimsNotchContext context)
        {
            // 非常や入力欠落で計算を抜けても、前回の出力を残さない。
            context.Output.resolvedBrakeStep = 0;
            context.Output.manualBrakeStepLabel = "B0-0";

            ResolveBrakeNotch(context);
            ResolvePowerNotch(context);

            context.Output.brakeStepLabel = TimsNotchCalculator.FormatBrakeNotchStep(
                context.Output.resolvedBrakeStep,
                context.Settings.brakeSubstepCount);
            context.Output.resolvedNotchLabel = context.Output.isEmergencyBrakeRequested ? "EB" :
                context.Output.resolvedBrakeStep > 0 ? context.Output.brakeStepLabel :
                context.Output.resolvedPowerNotch > 0 ? $"P{context.Output.resolvedPowerNotch}" : "N";
        }


        private static void ResolveBrakeNotch(TimsNotchContext context)
        {

            //　有効運転台がない場合は非常
            if (context.Input.cabActivationPosition == Equipment.Operation.CabActivationSwitch.CabActivationPosition.Off ||
                context.Input.carInputs.Count == 0)
            {
                context.Output.isEmergencyBrakeRequested = true;
                return;
            }

            TimsCabInput cabInput = null;
            if (context.Input.cabActivationPosition == Equipment.Operation.CabActivationSwitch.CabActivationPosition.Front)
            {
                if (context.Input.carInputs[0] != null)
                {
                    cabInput = context.Input.carInputs[0];
                }
            } else
            {
                if (context.Input.carInputs[^1] != null)
                {
                    cabInput = context.Input.carInputs[^1];
                }
            }

            // 有効運転台からの入力がない場合には非常
            if (cabInput == null)
            {
                context.Output.isEmergencyBrakeRequested = true;
                return;
            }

            // 入力が非常ブレーキの場合は非常
            if (
                context.Input.isAtcEmergency ||
                cabInput.brakeNotch >= context.Settings.masterControllerEmergencyBrakeNotchPosition
            )
            {
                context.Output.isEmergencyBrakeRequested = true;
                return;
            }


            TimsNotchCalculator.ToBrakeStep(
                cabInput.brakeNotch,
                0,
                context.Settings.brakeSubstepCount,
                out int manualBrakeStep
            );

            context.Output.manualBrakeStepLabel = TimsNotchCalculator.FormatBrakeNotchStep(
                manualBrakeStep,
                context.Settings.brakeSubstepCount);

            // 入力されたBrakeStepの最大値
            int maxInputBrakeStep = Mathf.Max(context.Input.atcBrakeStep, manualBrakeStep);

            // 許可されたBrakeStepの最大値
            TimsNotchCalculator.ToBrakeStep(
                context.Settings.brakeNotchCount,
                0,
                context.Settings.brakeSubstepCount,
                out int maxBrakeStep
            );

            // 入力されたBrakeStepが最大BrakeStepを超えてる場合は非常
            if (maxInputBrakeStep > maxBrakeStep)
            {
                context.Output.isEmergencyBrakeRequested = true;
                return;
            }

            context.Output.isEmergencyBrakeRequested = false;
            context.Output.resolvedBrakeStep = maxInputBrakeStep;

        }

        private static void ResolvePowerNotch(TimsNotchContext context)
        {
            // 前回の力行ノッチを残さないよう、力行なしで初期化する。
            context.Output.resolvedPowerNotch = 0;

            // 有効運転台がない場合は力行しない。
            if (context.Input.cabActivationPosition == Equipment.Operation.CabActivationSwitch.CabActivationPosition.Off ||
                context.Input.carInputs.Count == 0)
            {
                return;
            }

            TimsCabInput cabInput = null;
            if (context.Input.cabActivationPosition == Equipment.Operation.CabActivationSwitch.CabActivationPosition.Front)
            {
                if (context.Input.carInputs[0] != null)
                {
                    cabInput = context.Input.carInputs[0];
                }
            } else
            {
                if (context.Input.carInputs[^1] != null)
                {
                    cabInput = context.Input.carInputs[^1];
                }
            }

            // 有効運転台からの入力がない場合は力行しない。
            if (cabInput == null)
            {
                return;
            }

            // 非常ブレーキまたはブレーキ要求がある場合は力行しない。
            if (context.Output.isEmergencyBrakeRequested ||
                context.Input.isAtcEmergency ||
                cabInput.brakeNotch > 0 ||
                context.Input.atcBrakeStep > 0 ||
                context.Input.tascBrakeStep > 0 ||
                context.Output.resolvedBrakeStep > 0)
            {
                return;
            }

            // 手動とATOの力行ノッチのうち、大きい方を採用する。
            // ATOはまだ実装されていないので、手動だけ採用
            int maxInputPowerNotch = cabInput.powerNotch;

            // 設定された力行段数の範囲に収める。
            context.Output.resolvedPowerNotch = Mathf.Clamp(
                maxInputPowerNotch,
                0,
                context.Settings.powerNotchCount);
        }
    }
}
