using System;

namespace Nakatetsu.Train.Equipment.Brake.ControlDevice
{
    public static class BrakeControlDeviceLogic
    {
        public static void Calculate(BrakeControlDeviceContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            context.Output.hasOperationalCylinders = false;
            context.Output.targetPressureKPa = 0f;
            context.Output.maximumBrakeForceN = 0f;

            float totalEffectivePistonAreaM2 = 0f;
            float commonMaximumPressureKPa = float.MaxValue;

            foreach (BrakeControlCylinderInput cylinder in context.Input.cylinders)
            {
                if (cylinder == null) throw new ArgumentException("Each cylinder requires an input record.");
                if (!cylinder.isHealthy) continue;

                float maximumPressureKPa = Math.Max(0f, cylinder.maximumPressureKPa);
                float pistonAreaM2 = Math.Max(0f, cylinder.pistonAreaM2);
                float mechanicalEfficiency = Clamp(cylinder.mechanicalEfficiency, 0f, 1f);
                float effectivePistonAreaM2 = pistonAreaM2 * mechanicalEfficiency;
                if (maximumPressureKPa <= 0f || effectivePistonAreaM2 <= 0f) continue;

                context.Output.hasOperationalCylinders = true;
                totalEffectivePistonAreaM2 += effectivePistonAreaM2;
                commonMaximumPressureKPa = Math.Min(
                    commonMaximumPressureKPa,
                    maximumPressureKPa);
            }

            if (!context.Output.hasOperationalCylinders || totalEffectivePistonAreaM2 <= 0f)
            {
                return;
            }

            context.Output.maximumBrakeForceN =
                commonMaximumPressureKPa * 1000f * totalEffectivePistonAreaM2;
            float requestedPressureKPa =
                Math.Max(0f, context.Input.targetBrakeForceN) /
                (1000f * totalEffectivePistonAreaM2);
            context.Output.targetPressureKPa = Math.Min(
                requestedPressureKPa,
                commonMaximumPressureKPa);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
