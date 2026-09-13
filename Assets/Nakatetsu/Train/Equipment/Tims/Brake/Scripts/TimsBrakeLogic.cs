using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Internal;

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

            int count = context.Input.cars.Count;
            context.Workspace.carMassesKg.Clear();

            foreach (var car in context.Input.cars)
            {
                context.Workspace.carMassesKg.Add(car.massKg);
            }

            while (context.Output.carCommands.Count < count)
            {
                context.Output.carCommands.Add(new TimsBrakeCarCommand());
            }

            if (context.Output.carCommands.Count > count)
            {
                context.Output.carCommands.RemoveRange(count, context.Output.carCommands.Count - count);
            }

            context.Output.totalMassKg = GetTotalMassKg(context);
            CalculateBrakeForces(context);
            context.Output.hasCommands = true;
        }

        private static float GetTotalMassKg(TimsBrakeContext context)
        {
            float total = 0f;
            foreach (float massKg in context.Workspace.carMassesKg)
            {
                total += massKg;
            }

            return total;
        }

        private static bool IsVvvfMotorCar(TimsBrakeContext context, int carIndex) => context.Input.cars[carIndex].isVvvfMotorCar;

        private static bool IsTrailerCar(TimsBrakeContext context, int carIndex) => context.Input.cars[carIndex].isTrailerCar;

        private static void EnsureFloatListSize(List<float> values, int count)
        {
            while (values.Count < count)
            {
                values.Add(0f);
            }

            if (values.Count > count)
            {
                values.RemoveRange(count, values.Count - count);
            }
        }

        private static void CalculateRegenPattern(TimsBrakeContext context, float remainingTargetBrakeForceN)
        {
            EnsureFloatListSize(context.Workspace.targetRegenForcesN, context.Workspace.carMassesKg.Count);

            float regenTotalMassKg = 0f;
            for (int i = 0; i < context.Workspace.carMassesKg.Count; i++)
            {
                if (!IsVvvfMotorCar(context, i))
                {
                    continue;
                }

                regenTotalMassKg += context.Workspace.carMassesKg[i];
            }

            for (int i = 0; i < context.Workspace.carMassesKg.Count; i++)
            {
                if (!IsVvvfMotorCar(context, i) || regenTotalMassKg <= 0f)
                {
                    context.Workspace.targetRegenForcesN[i] = 0f;
                    continue;
                }

                context.Workspace.targetRegenForcesN[i] =
                    Math.Max(0f, context.Workspace.carMassesKg[i]) /
                    regenTotalMassKg * remainingTargetBrakeForceN;
            }
        }

        private static void CalculateBrakeForces(TimsBrakeContext context)
        {
            int subStepCount = context.Settings.brakeSubstepCount;
            List<float> decelerationsMps2 = context.Settings.brakeTargetDecelerationsMps2;

            float targetDecelerationMps2 = TimsBrakeCalculator.GetBrakeDecelerationFromStep(
                context.Input.brakeStep,
                subStepCount,
                decelerationsMps2
            );

            context.Output.targetTotalBrakeForceN = GetTotalMassKg(context) * targetDecelerationMps2;
            float minimumAirTotalForceN = CalculateMinimumAirBrakeForces(context);
            float remainingTargetBrakeForceN = Math.Max(
                0f,
                context.Output.targetTotalBrakeForceN - minimumAirTotalForceN);

            // 回生PTN計算
            CalculateRegenPattern(context, remainingTargetBrakeForceN);

            // 最低込め圧を除いた追加ブレーキ目標を質量比で計算
            CalculateTargetCarBrakeForcesN(context, remainingTargetBrakeForceN);

            CalculateAirBrakeForces(context);
            UpdateBrakeForceCommands(context);
        }

        private static void CalculateTargetCarBrakeForcesN(TimsBrakeContext context, float targetBrakeForceN)
        {
            EnsureFloatListSize(context.Workspace.targetCarBrakeForcesN, context.Workspace.carMassesKg.Count);
            float totalMassKg = Math.Max(1f, GetTotalMassKg(context));

            for (int i = 0; i < context.Workspace.carMassesKg.Count; i++)
            {
                context.Workspace.targetCarBrakeForcesN[i] =
                    Math.Max(0f, context.Workspace.carMassesKg[i]) /
                    totalMassKg * targetBrakeForceN;
            }
        }

        private static float CalculateMinimumAirBrakeForces(TimsBrakeContext context)
        {
            EnsureFloatListSize(context.Workspace.minimumAirForcesN, context.Input.cars.Count);

            float total = 0f;
            float baseMinimumPressureKPa = context.Input.brakeStep > 0
                ? Math.Max(0f, context.Settings.minimumServiceBrakePressureKPa)
                : 0f;
            float minMassKg = GetMinimumCarMassKg(context);
            float maxLoadScale = Math.Max(1f, context.Settings.minimumServiceBrakePressureLoadScaleMax);

            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                TimsBrakeCarInput carBrakeOutput = context.Input.cars[i];
                float maxPressureKPa = carBrakeOutput != null
                    ? Math.Max(0f, carBrakeOutput.maxBCPressureKPa)
                    : 0f;
                float forcePerKPa = carBrakeOutput != null
                    ? Math.Max(0f, carBrakeOutput.airForcePerKPa)
                    : 0f;
                float loadScale = minMassKg > 0f && i < context.Workspace.carMassesKg.Count
                    ? TimsMath.Clamp(Math.Max(0f, context.Workspace.carMassesKg[i]) / minMassKg, 1f, maxLoadScale)
                    : 1f;
                float minimumPressureKPa = baseMinimumPressureKPa * loadScale;
                float pressureKPa = maxPressureKPa > 0f
                    ? Math.Min(minimumPressureKPa, maxPressureKPa)
                    : minimumPressureKPa;

                context.Workspace.minimumAirForcesN[i] = pressureKPa * forcePerKPa;
                total += context.Workspace.minimumAirForcesN[i];
            }

            return total;
        }

        private static float GetMinimumCarMassKg(TimsBrakeContext context)
        {
            float minMassKg = float.MaxValue;

            for (int i = 0; i < context.Workspace.carMassesKg.Count; i++)
            {
                float massKg = Math.Max(0f, context.Workspace.carMassesKg[i]);
                if (massKg > 0f)
                {
                    minMassKg = Math.Min(minMassKg, massKg);
                }
            }

            return minMassKg < float.MaxValue ? minMassKg : 0f;
        }

        private static void CalculateAirBrakeForces(TimsBrakeContext context)
        {
            EnsureFloatListSize(context.Workspace.additionalAirForcesN, context.Input.cars.Count);

            float regenSurplusForceN = 0f;
            context.Output.actualRegenTotalForceN = 0f;
            for (int i = 0; i < context.Input.cars.Count; i++)
            {
                TimsBrakeCarInput carBrakeOutput = context.Input.cars[i];
                float actualRegenForceN = carBrakeOutput != null
                    ? Math.Max(0f, carBrakeOutput.regenForceN)
                    : 0f;
                float targetBrakeForceN = GetTargetCarBrakeForceN(context, i);

                context.Output.actualRegenTotalForceN += actualRegenForceN;

                if (IsVvvfMotorCar(context, i))
                {
                    if (actualRegenForceN >= targetBrakeForceN)
                    {
                        regenSurplusForceN += actualRegenForceN - targetBrakeForceN;
                        context.Workspace.additionalAirForcesN[i] = 0f;
                    }
                    else
                    {
                        context.Workspace.additionalAirForcesN[i] = ClampAdditionalAirForceN(
                            context,
                            i,
                            targetBrakeForceN - actualRegenForceN);
                    }
                }
                else
                {
                    context.Workspace.additionalAirForcesN[i] = ClampAdditionalAirForceN(
                        context,
                        i,
                        targetBrakeForceN);
                }
            }

            ReduceTrailerAirBrakeByRegenSurplus(context, regenSurplusForceN);
        }

        private static void ReduceTrailerAirBrakeByRegenSurplus(TimsBrakeContext context, float regenSurplusForceN)
        {
            if (regenSurplusForceN <= 0f)
            {
                return;
            }

            List<float> trailerReductionCaps = new();
            for (int i = 0; i < context.Workspace.additionalAirForcesN.Count; i++)
            {
                trailerReductionCaps.Add(
                    IsTrailerCar(context, i)
                        ? context.Workspace.additionalAirForcesN[i]
                        : 0f);
            }

            List<float> reductions = TimsBrakeCalculator.AllocateEvenlyWithSaturation(
                trailerReductionCaps,
                regenSurplusForceN
            );

            for (int i = 0; i < context.Workspace.additionalAirForcesN.Count; i++)
            {
                context.Workspace.additionalAirForcesN[i] = Math.Max(
                    0f,
                    context.Workspace.additionalAirForcesN[i] - reductions[i]);
            }
        }

        private static float ClampAdditionalAirForceN(TimsBrakeContext context, int carIndex, float additionalAirTargetN)
        {
            float minimumAirForceN = GetMinimumAirForceN(context, carIndex);
            float airCapN = GetAirCapForceN(context, carIndex);
            float additionalAirCapN = Math.Max(0f, airCapN - minimumAirForceN);

            return additionalAirCapN > 0f
                ? TimsMath.Clamp(additionalAirTargetN, 0f, additionalAirCapN)
                : Math.Max(0f, additionalAirTargetN);
        }

        private static float GetTargetCarBrakeForceN(TimsBrakeContext context, int carIndex)
        {
            return carIndex >= 0 && carIndex < context.Workspace.targetCarBrakeForcesN.Count
                ? Math.Max(0f, context.Workspace.targetCarBrakeForcesN[carIndex])
                : 0f;
        }

        private static float GetMinimumAirForceN(TimsBrakeContext context, int carIndex)
        {
            return carIndex >= 0 && carIndex < context.Workspace.minimumAirForcesN.Count
                ? Math.Max(0f, context.Workspace.minimumAirForcesN[carIndex])
                : 0f;
        }

        private static float GetAirCapForceN(TimsBrakeContext context, int carIndex)
        {
            if (carIndex < 0 ||
                carIndex >= context.Input.cars.Count ||
                context.Input.cars[carIndex] == null)
            {
                return 0f;
            }

            return Math.Max(0f, context.Input.cars[carIndex].airCapN);
        }

        private static void UpdateBrakeForceCommands(TimsBrakeContext context)
        {
            for (int i = 0; i < context.Output.carCommands.Count; i++)
            {
                TimsBrakeCarCommand command = context.Output.carCommands[i];
                float targetRegenForceN = i < context.Workspace.targetRegenForcesN.Count
                    ? context.Workspace.targetRegenForcesN[i]
                    : 0f;
                float targetAirForceN = i < context.Workspace.additionalAirForcesN.Count
                    ? context.Workspace.additionalAirForcesN[i]
                    : 0f;
                float minimumAirForceN = i < context.Workspace.minimumAirForcesN.Count
                    ? context.Workspace.minimumAirForcesN[i]
                    : 0f;

                command.targetRegenForceN = targetRegenForceN;
                command.targetAirForceN = targetAirForceN + minimumAirForceN;
                command.targetBrakeForceN = GetTargetCarBrakeForceN(context, i);
                command.isEmergency = false;
            }
        }
    }
}
