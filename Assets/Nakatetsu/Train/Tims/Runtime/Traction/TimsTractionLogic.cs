using System;

namespace Nakatetsu.Train.Tims.Traction
{
    public static class TimsTractionLogic
    {
        public static void Calculate(TimsTractionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var input = context.Input;
            var output = context.Output;
            output.hasForceCommand = false;
            if (!input.isReady)
            {
                output.targetForceN = 0f;
                output.ratedConsistPowerW = 0f;
                output.targetAccelerationMps2 = 0f;
                output.constantAccelerationEndSpeedMps = 0f;
                output.regionLabel = "--";
                output.isBCReleaseInterlockActive = false;
                ClearSpeedHold(context);
                DistributeTargetForce(context, 0f);
                return;
            }
            if (input.brakeStep > 0)
            {
                output.targetForceN = 0f;
                output.targetForcePerVvvfN = 0f;
                output.activeVvvfCount = 0;
                output.regionLabel = "Brake";
                output.isBCReleaseInterlockActive = false;
                ClearSpeedHold(context);
                return;
            }
            UpdateSpeedHold(context);
            float powerW = 0f;
            foreach (var unit in input.units)
                if (unit.isAvailable && unit.hasMotorSettings)
                    powerW += unit.ratedMotorPowerW * unit.motorCount;
            output.ratedConsistPowerW = powerW;
            output.targetAccelerationMps2 = Math.Max(0f, context.Settings.launchAccelerationMps2);
            output.constantAccelerationEndSpeedMps = CalculateConstantAccelerationEndSpeedMps(
                powerW, input.consistMassKg, output.targetAccelerationMps2);
            float maxForceN;
            if (input.speedMps <= output.constantAccelerationEndSpeedMps)
            {
                output.regionLabel = "Const Accel";
                maxForceN = input.consistMassKg * output.targetAccelerationMps2;
            }
            else
            {
                output.regionLabel = "Const Power";
                maxForceN = powerW / Math.Max(0.1f, input.speedMps);
            }
            output.targetForceN = context.State.speedHoldMode == TimsSpeedHoldMode.Arming || input.powerNotch <= 0
                ? 0f : maxForceN * Math.Max(0f, input.powerStepGain);
            output.isBCReleaseInterlockActive = !AreAllBCReleased(context);
            if (output.isBCReleaseInterlockActive)
            {
                output.targetForceN = 0f;
                output.regionLabel = "BC Interlock";
            }
            DistributeTargetForce(context, output.targetForceN);
        }

        public static float CalculateConstantAccelerationEndSpeedMps(float ratedConsistPowerW,
            float massKg, float targetAccelerationMps2) =>
            Math.Max(0f, ratedConsistPowerW) / (Math.Max(1f, massKg) * Math.Max(0.01f, targetAccelerationMps2));

        private static void UpdateSpeedHold(TimsTractionContext context)
        {
            var state = context.State;
            var input = context.Input;
            if (state.speedHoldMode == TimsSpeedHoldMode.Off) return;
            if (input.manualPowerNotch != 2 || input.manualBrakeNotch > 0 || input.atcBrakeNotch > 0)
            {
                ClearSpeedHold(context);
                return;
            }
            if (state.speedHoldMode == TimsSpeedHoldMode.Arming)
            {
                state.speedHoldArmingTimerSeconds += Math.Max(0f, input.deltaTimeSeconds);
                if (state.speedHoldArmingTimerSeconds >= context.Settings.speedHoldArmingSeconds)
                {
                    state.speedHoldTargetMps = input.speedMps;
                    state.speedHoldMode = TimsSpeedHoldMode.Active;
                }
            }
        }

        private static void ClearSpeedHold(TimsTractionContext context)
        {
            context.State.speedHoldMode = TimsSpeedHoldMode.Off;
            context.State.speedHoldArmingTimerSeconds = 0f;
            context.State.speedHoldTargetMps = 0f;
        }

        private static bool AreAllBCReleased(TimsTractionContext context)
        {
            if (context.Input.isGradientStart) return true;
            if (context.Input.carBCPressuresKPa.Count == 0)
                return context.Input.currentBCPressureKPa < context.Settings.bcReleaseThresholdKPa;
            foreach (float pressureKPa in context.Input.carBCPressuresKPa)
                if (pressureKPa >= context.Settings.bcReleaseThresholdKPa) return false;
            return true;
        }

        private static void DistributeTargetForce(TimsTractionContext context, float totalForceN)
        {
            var output = context.Output;
            output.hasForceCommand = true;
            output.activeVvvfCount = 0;
            foreach (var unit in context.Input.units)
                if (unit.isAvailable) output.activeVvvfCount++;
            output.targetForcePerVvvfN = output.activeVvvfCount > 0 ? totalForceN / output.activeVvvfCount : 0f;
            output.unitTargetForcesN.Clear();
            foreach (var unit in context.Input.units)
                output.unitTargetForcesN.Add(unit.isAvailable ? output.targetForcePerVvvfN : 0f);
        }
    }
}
