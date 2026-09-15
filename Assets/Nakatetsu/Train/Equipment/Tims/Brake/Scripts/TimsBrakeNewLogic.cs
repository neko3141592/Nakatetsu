using System;
using System.Collections.Generic;
using System.Linq;
using Nakatetsu.Train.Equipment.Tims.Internal;
using Nakatetsu.Train.Equipment.Tims.Notch;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    public static class TimsBrakeNewLogic
    {
        public static void Calculate(TimsBrakeContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            context.Output.isEmergency = !context.Input.canReleaseEmergencyBrake;
            context.Output.hasCommands = false;

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
    }
}
