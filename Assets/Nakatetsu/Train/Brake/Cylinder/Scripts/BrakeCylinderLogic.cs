using System;

namespace Nakatetsu.Train.Brake.Cylinder
{
    public static class BrakeCylinderLogic
    {
        public static void Calculate(BrakeCylinderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            float maximumPressureKPa = Math.Max(0f, context.Settings.maximumPressureKPa);
            float currentPressureKPa = Clamp(
                context.State.currentPressureKPa,
                0f,
                maximumPressureKPa);
            float targetPressureKPa = context.State.isHealthy
                ? Clamp(context.Input.targetPressureKPa, 0f, maximumPressureKPa)
                : 0f;
            float rateKPaPerSecond = targetPressureKPa > currentPressureKPa
                ? Math.Max(0f, context.Settings.applyRateKPaPerSecond)
                : Math.Max(0f, context.Settings.releaseRateKPaPerSecond);
            float maximumChangeKPa = rateKPaPerSecond * Math.Max(0f, context.Input.deltaTimeSeconds);

            context.State.currentPressureKPa = MoveTowards(
                currentPressureKPa,
                targetPressureKPa,
                maximumChangeKPa);

            float pistonAreaM2 = Math.Max(0f, context.Settings.pistonAreaM2);
            float mechanicalEfficiency = Clamp(context.Settings.mechanicalEfficiency, 0f, 1f);
            context.Output.actualForceN =
                context.State.currentPressureKPa * 1000f * pistonAreaM2 * mechanicalEfficiency;
        }

        private static float MoveTowards(float current, float target, float maximumChange)
        {
            if (current < target) return Math.Min(current + maximumChange, target);
            if (current > target) return Math.Max(current - maximumChange, target);
            return target;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
