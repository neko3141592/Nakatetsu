using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Notch;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    public static class TimsBrakeLogic
    {
        public static void Calculate(TimsBrakeContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            context.Output.isEmergency = !context.Input.canReleaseEmergencyBrake;
            context.Output.hasCommands = false;
            context.Output.carCommands.Clear();

            if (context.Output.isEmergency)
            {
                return;
            }

            foreach (var car in context.Input.cars)
            {
                if (car == null)
                {
                    throw new ArgumentException("Each car requires an input record.");
                }
            }

            InitializeWorkSpace(context);

            // 必要減速度を計算
            CalculateTargetDeceleration(context);

            // 必要ブレーキ力を計算
            CalculateTargetTotalBrakeForce(context);

            // 最低込め圧を込める
            CalculateMinimumAirBrakePressure(context);

            // 最低込め分を除いた必要ブレーキ力を計算
            CalculateRemainingTargetBrakeForce(context);

            // 最低込め分を除いた必要ブレーキ力を各車の質量比で配分
            CalculateTargetCarBrakeForces(context);

            // 最低込め分を除いた必要ブレーキ力をVVVF搭載車へ回生目標として均等配分
            CalculateTargetRegenForces(context);

            // 自車の実回生力と他車の余剰回生力を差し引き、追加空制力を計算
            CalculateAdditionalAirBrakeForces(context);

            // 各車の空制力をBC圧へ変換し、指令を出力する。
            CalculateOutput(context);
        }

        public static void InitializeWorkSpace(TimsBrakeContext context)
        {
            context.Workspace.targetTotalBrakeForceN = 0f;
            context.Workspace.remainingTargetBrakeForceN = 0f;
            context.Workspace.targetDecelerationMps2 = 0f;

            context.Workspace.minimumAirPressureKPa = 0f;

            context.Workspace.targetCarBrakeForcesN.Clear();
            context.Workspace.targetRegenForcesN.Clear();
            context.Workspace.additionalAirForcesN.Clear();
            context.Workspace.minimumAirForcesN.Clear();

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                context.Workspace.targetCarBrakeForcesN.Add(0f);
                context.Workspace.targetRegenForcesN.Add(0f);
                context.Workspace.additionalAirForcesN.Add(0f);
                context.Workspace.minimumAirForcesN.Add(0f);
            }


        }

        public static void CalculateTargetDeceleration(TimsBrakeContext context)
        {

            if (context.Input.brakeStep == 0)
            {
                context.Workspace.targetDecelerationMps2 = 0f;
                return;
            }


            TimsNotchCalculator.ToBrakeNotchStep(
                context.Input.brakeStep,
                context.Settings.brakeSubstepCount,
                out int brakeNotch,
                out int notchStep
            );

            int maxBrakeNotch = context.Settings.brakeTargetDecelerationsMps2.Count;

            if (brakeNotch == maxBrakeNotch)
            {
                context.Workspace.targetDecelerationMps2 = context.Settings.brakeTargetDecelerationsMps2[brakeNotch - 1];
            }
            else
            {
                float previousTargetDecelerationMps2 = context.Settings.brakeTargetDecelerationsMps2[brakeNotch - 1];
                float nextTargetDecelerationMps2 = context.Settings.brakeTargetDecelerationsMps2[brakeNotch];
                float decelerationDifferenceMps2 = Mathf.Max(nextTargetDecelerationMps2 - previousTargetDecelerationMps2, 0f);

                context.Workspace.targetDecelerationMps2 =
                    previousTargetDecelerationMps2 +
                    decelerationDifferenceMps2 / context.Settings.brakeSubstepCount * notchStep;
            }
        }
        public static void CalculateTargetTotalBrakeForce(TimsBrakeContext context)
        {
            float totalMassKg = 0f;

            foreach (TimsBrakeCarInput carInput in context.Input.cars)
            {
                totalMassKg += carInput.massKg;
            }

            context.Workspace.targetTotalBrakeForceN = totalMassKg * context.Workspace.targetDecelerationMps2;
        }

        public static void CalculateMinimumAirBrakePressure(TimsBrakeContext context)
        {
            context.Workspace.minimumAirPressureKPa = context.Input.brakeStep > 0
                ? Mathf.Max(0f, context.Settings.minimumServiceBrakePressureKPa)
                : 0f;

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                context.Workspace.minimumAirForcesN[i] =
                    context.Workspace.minimumAirPressureKPa *
                    Mathf.Max(0f, context.Input.cars[i].airForcePerKPa);
            }
        }

        public static void CalculateRemainingTargetBrakeForce(TimsBrakeContext context)
        {
            float minimumAirTotalForceN = 0f;

            foreach (float minimumAirForceN in context.Workspace.minimumAirForcesN)
            {
                minimumAirTotalForceN += minimumAirForceN;
            }

            context.Workspace.remainingTargetBrakeForceN = Mathf.Max(
                context.Workspace.targetTotalBrakeForceN - minimumAirTotalForceN,
                0f);
        }

        public static void CalculateOutput(TimsBrakeContext context)
        {
            context.Output.hasCommands = false;
            context.Output.carCommands.Clear();

            if (context.Output.isEmergency)
            {
                return;
            }

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                TimsBrakeCarInput carInput = context.Input.cars[i];
                float targetAirForceN = Mathf.Max(
                    context.Workspace.minimumAirForcesN[i] +
                    context.Workspace.additionalAirForcesN[i],
                    0f);
                targetAirForceN = Mathf.Min(targetAirForceN, Mathf.Max(0f, carInput.airCapN));

                // airForcePerKPaはN/kPa。Fを係数で割るとkPaになる。
                float targetAirPressureKPa = carInput.airForcePerKPa > 0f
                    ? Mathf.Clamp(targetAirForceN / carInput.airForcePerKPa,
                        0f, Mathf.Max(0f, carInput.maxBCPressureKPa))
                    : 0f;

                // 圧力上限適用後の値に合わせ、力と圧力の指令を一致させる。
                targetAirForceN = carInput.airForcePerKPa > 0f
                    ? targetAirPressureKPa * carInput.airForcePerKPa
                    : 0f;

                context.Output.carCommands.Add(new TimsBrakeCarCommand
                {
                    targetRegenForceN = context.Workspace.targetRegenForcesN[i],
                    targetAirForceN = targetAirForceN,
                    targetAirPressureKPa = targetAirPressureKPa,
                    targetBrakeForceN = context.Workspace.targetCarBrakeForcesN[i],
                    isEmergency = false
                });
            }

            context.Output.hasCommands = true;
        }

        public static void CalculateTargetCarBrakeForces(TimsBrakeContext context)
        {
            float totalMassKg = 0f;

            foreach (TimsBrakeCarInput carInput in context.Input.cars)
            {
                totalMassKg += Mathf.Max(0f, carInput.massKg);
            }

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                context.Workspace.targetCarBrakeForcesN[i] = totalMassKg > 0f
                    ? context.Workspace.remainingTargetBrakeForceN *
                      Mathf.Max(0f, context.Input.cars[i].massKg) / totalMassKg
                    : 0f;
            }
        }

        public static void CalculateTargetRegenForces(TimsBrakeContext context)
        {
            List<float> regenCapsN = new();

            foreach (TimsBrakeCarInput carInput in context.Input.cars)
            {
                regenCapsN.Add(carInput.isVvvfMotorCar
                    ? Mathf.Max(0f, carInput.regenCapN)
                    : 0f);
            }

            // 上限に達した車両の残りを、余力のある車両へ均等に再配分する。
            List<float> targetRegenForcesN = TimsBrakeCalculator.AllocateEvenlyWithSaturation(
                regenCapsN,
                context.Workspace.remainingTargetBrakeForceN);

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                context.Workspace.targetRegenForcesN[i] = targetRegenForcesN[i];
            }
        }

        public static void CalculateAdditionalAirBrakeForces(TimsBrakeContext context)
        {
            float regenSurplusForceN = 0f;

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                // 回生目標ではなく、実際に出た回生力を使う。
                float actualRegenForceN = Mathf.Max(0f, context.Input.cars[i].regenForceN);
                float targetCarBrakeForceN = context.Workspace.targetCarBrakeForcesN[i];

                context.Workspace.additionalAirForcesN[i] = Mathf.Max(
                    targetCarBrakeForceN - actualRegenForceN,
                    0f);
                regenSurplusForceN += Mathf.Max(
                    actualRegenForceN - targetCarBrakeForceN,
                    0f);
            }

            // 他車の追加空制分を上限に、余剰回生を均等に充当する。
            // 最低込め分のminimumAirForcesNは減らさない。
            List<float> reductionsN = TimsBrakeCalculator.AllocateEvenlyWithSaturation(
                context.Workspace.additionalAirForcesN,
                regenSurplusForceN);

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                context.Workspace.additionalAirForcesN[i] = Mathf.Max(
                    context.Workspace.additionalAirForcesN[i] - reductionsN[i],
                    0f);
            }
        }
    }
}
