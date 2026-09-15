using System;

namespace Nakatetsu.Train.Equipment.SpeedMeasurement
{
    public static class SpeedSensorLogic
    {
        public static void Calculate(SpeedSensorContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            float speed = context.Input.signedPhysicalSpeedMps;
            context.Output.hasMeasurement = context.Input.hasPhysicalSpeed &&
                !float.IsNaN(speed) && !float.IsInfinity(speed);
            context.Output.measuredSpeedMps = context.Output.hasMeasurement ? Math.Abs(speed) : 0f;
        }
    }
}
