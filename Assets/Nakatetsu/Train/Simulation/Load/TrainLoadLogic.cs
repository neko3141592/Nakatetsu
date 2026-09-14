using System;

namespace Nakatetsu.Train.Simulation.Load
{
    public static class TrainLoadLogic
    {
        public static void Calculate(TrainLoadContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int passengerCapacity = Math.Max(0, context.Settings.passengerCapacity);
            int actualPassengerCount = Math.Min(
                Math.Max(0, context.Input.passengerCount),
                passengerCapacity);
            float actualPassengerMassKg =
                actualPassengerCount * Math.Max(0f, context.Settings.averagePassengerMassKg);
            float actualCargoMassKg = Math.Max(0f, context.Input.cargoMassKg);
            float actualTotalMassKg =
                Math.Max(0f, context.Settings.emptyMassKg) +
                actualPassengerMassKg +
                actualCargoMassKg;

            TrainLoadOutput output = context.Output;
            output.actualPassengerCount = actualPassengerCount;
            output.actualPassengerMassKg = actualPassengerMassKg;
            output.actualCargoMassKg = actualCargoMassKg;
            output.actualTotalMassKg = actualTotalMassKg;

            // 空気ばね・ばね下質量を分離するまでは、実総質量を実支持質量とする。
            output.actualSupportedMassKg = actualTotalMassKg;
        }

        public static void ApplyOutput(TrainLoadContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            TrainLoadOutput output = context.Output;
            TrainLoadState state = context.State;
            state.actualPassengerCount = output.actualPassengerCount;
            state.actualPassengerMassKg = output.actualPassengerMassKg;
            state.actualCargoMassKg = output.actualCargoMassKg;
            state.actualTotalMassKg = output.actualTotalMassKg;
            state.actualSupportedMassKg = output.actualSupportedMassKg;
            state.isInitialized = true;
        }
    }
}
