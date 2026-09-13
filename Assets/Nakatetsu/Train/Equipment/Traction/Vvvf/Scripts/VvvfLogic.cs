using System;
using UnityEngine;
using Nakatetsu.Train.Equipment.Traction.Motor;

namespace Nakatetsu.Train.Equipment.Traction.Vvvf
{
    public static class VvvfLogic
    {
        public static void Calculate(VvvfContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            VvvfSettings settings = context.Settings;
            VvvfState state = context.State;
            VvvfInput input = context.Input;
            VvvfOutput output = context.Output;
            float deltaTimeSeconds = Mathf.Max(0f, input.deltaTimeSeconds);

            output.driveMode = ResolveDriveMode(input.targetTractionForceN);
            output.wheelRpm = GetWheelRpm(Mathf.Abs(input.vehicleSpeedMps), input.wheelRadiusM);
            output.motorRpm = output.wheelRpm * input.gearRatio;
            output.rotorBaseFrequencyHz = MotorLogic.GetFrequencyFromSynchronousRpm(
                output.motorRpm,
                input.motorPoleCount);
            output.targetMotorTorqueNm = CalculateTargetMotorTorqueNm(input);

            UpdateSlipFrequency(context, deltaTimeSeconds);

            bool hasDriveCommand = output.driveMode != VvvfDriveMode.Neutral;
            bool isOutputDecaying = state.voltageRatio > 0.001f ||
                Mathf.Abs(state.slipFrequencyHz) > 0.01f;
            float targetFrequencyHz = output.rotorBaseFrequencyHz + state.slipFrequencyHz;
            if (output.driveMode == VvvfDriveMode.Power && output.motorRpm < 1f)
            {
                targetFrequencyHz = Mathf.Max(settings.launchFrequencyHz, targetFrequencyHz);
            }

            output.frequencyHz = hasDriveCommand || isOutputDecaying
                ? Mathf.Max(0f, targetFrequencyHz)
                : 0f;
            output.synchronousRpm = MotorLogic.GetSynchronousRpm(
                output.frequencyHz,
                input.motorPoleCount);
            output.slipRatio = MotorLogic.GetSlipRatio(
                output.synchronousRpm,
                output.motorRpm);

            float targetVoltageRatio = 0f;
            if (hasDriveCommand)
            {
                targetVoltageRatio = Mathf.Clamp01(
                    output.frequencyHz / Mathf.Max(0.01f, input.ratedMotorFrequencyHz));
                if (output.driveMode == VvvfDriveMode.Power)
                {
                    targetVoltageRatio = Mathf.Max(
                        targetVoltageRatio,
                        settings.launchVoltageBoostRatio);
                }
            }

            state.voltageRatio = Mathf.MoveTowards(
                state.voltageRatio,
                targetVoltageRatio,
                settings.voltageControlRateRatioPerSecond * deltaTimeSeconds);
            if (state.voltageRatio < 0.0001f) state.voltageRatio = 0f;

            output.lineVoltageRmsV = input.ratedMotorLineVoltageV * state.voltageRatio;
            UpdateThreePhaseWave(context, deltaTimeSeconds);
        }

        private static void UpdateSlipFrequency(VvvfContext context, float deltaTimeSeconds)
        {
            VvvfSettings settings = context.Settings;
            VvvfState state = context.State;
            VvvfOutput output = context.Output;
            float changeRate = settings.slipFrequencyControlRateHzPerSecond *
                deltaTimeSeconds * Mathf.Max(0f, state.responseVariation);

            if (output.driveMode == VvvfDriveMode.Neutral)
            {
                state.slipFrequencyHz = Mathf.MoveTowards(state.slipFrequencyHz, 0f, changeRate);
                return;
            }

            float torqueErrorNm = output.targetMotorTorqueNm -
                context.Input.representativeMotorTorqueNm;
            if (Mathf.Abs(torqueErrorNm) <= settings.torqueDeadbandNm) return;

            bool isPower = output.driveMode == VvvfDriveMode.Power;
            float maximumSlipFrequencyHz = GetSpeedLimitedMaximumSlipFrequencyHz(
                context,
                isPower);
            float targetSlipFrequencyHz = isPower
                ? (torqueErrorNm > 0f ? maximumSlipFrequencyHz : 0f)
                : (torqueErrorNm < 0f ? -maximumSlipFrequencyHz : 0f);

            state.slipFrequencyHz = Mathf.MoveTowards(
                state.slipFrequencyHz,
                targetSlipFrequencyHz,
                changeRate);
            state.slipFrequencyHz = Mathf.Clamp(
                state.slipFrequencyHz,
                -maximumSlipFrequencyHz,
                maximumSlipFrequencyHz);
        }

        private static float GetSpeedLimitedMaximumSlipFrequencyHz(
            VvvfContext context,
            bool allowLaunchSlip)
        {
            VvvfSettings settings = context.Settings;
            float speedLimitedHz = Mathf.Max(0f, context.Output.rotorBaseFrequencyHz) *
                settings.maximumSlipRatio;
            if (allowLaunchSlip)
            {
                speedLimitedHz = Mathf.Max(settings.launchSlipFrequencyHz, speedLimitedHz);
            }

            return Mathf.Min(settings.maximumSlipFrequencyHz, speedLimitedHz);
        }

        private static void UpdateThreePhaseWave(VvvfContext context, float deltaTimeSeconds)
        {
            VvvfState state = context.State;
            VvvfOutput output = context.Output;
            output.phaseVoltagePeakV = output.lineVoltageRmsV / Mathf.Sqrt(3f) * Mathf.Sqrt(2f);
            state.phaseRad = Mathf.Repeat(
                state.phaseRad + 2f * Mathf.PI * output.frequencyHz * deltaTimeSeconds,
                2f * Mathf.PI);
            output.uPhaseV = output.phaseVoltagePeakV * Mathf.Sin(state.phaseRad);
            output.vPhaseV = output.phaseVoltagePeakV *
                Mathf.Sin(state.phaseRad - 2f * Mathf.PI / 3f);
            output.wPhaseV = output.phaseVoltagePeakV *
                Mathf.Sin(state.phaseRad + 2f * Mathf.PI / 3f);
        }

        private static float GetWheelRpm(float speedMps, float wheelRadiusM)
        {
            float angularSpeedRadPerSecond = speedMps / Mathf.Max(0.01f, wheelRadiusM);
            return angularSpeedRadPerSecond * 60f / (2f * Mathf.PI);
        }

        private static float CalculateTargetMotorTorqueNm(VvvfInput input)
        {
            float denominator = input.gearRatio * input.transmissionEfficiency *
                Mathf.Max(1, input.motorCount);
            return input.targetTractionForceN * input.wheelRadiusM /
                Mathf.Max(0.01f, denominator);
        }

        private static VvvfDriveMode ResolveDriveMode(float targetTractionForceN)
        {
            if (targetTractionForceN > 0.01f) return VvvfDriveMode.Power;
            if (targetTractionForceN < -0.01f) return VvvfDriveMode.Regen;
            return VvvfDriveMode.Neutral;
        }
    }
}
