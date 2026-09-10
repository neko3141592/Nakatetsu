using System;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Brake
{
    public static class TimsBrakeBus
    {
        public static readonly TimsTagKey TargetAirBrakeForcesNKey =
            new("Brake", "TargetAirBrakeForcesN");
        public static readonly TimsTagKey IsEmergencyKey =
            new("Brake", "IsEmergency");

        public static void Publish(TimsBusState masterBus, TimsBrakeOutput output)
        {
            if (masterBus == null) throw new ArgumentNullException(nameof(masterBus));
            if (output == null) throw new ArgumentNullException(nameof(output));

            masterBus.SetBool(IsEmergencyKey, output.isEmergency);
            if (!output.hasCommands)
            {
                masterBus.Remove(TargetAirBrakeForcesNKey);
                return;
            }

            var targetAirBrakeForcesN = new float[output.carCommands.Count];
            for (int carIndex = 0; carIndex < output.carCommands.Count; carIndex++)
            {
                TimsBrakeCarCommand command = output.carCommands[carIndex];
                targetAirBrakeForcesN[carIndex] = command != null
                    ? Math.Max(0f, command.targetAirForceN)
                    : 0f;
            }

            masterBus.SetFloatArray(TargetAirBrakeForcesNKey, targetAirBrakeForcesN);
        }

        public static bool TryGetTargetAirBrakeForceN(
            TimsBusState masterBus,
            int carIndex,
            out float targetAirBrakeForceN)
        {
            targetAirBrakeForceN = 0f;
            if (masterBus == null || carIndex < 0) return false;
            if (!masterBus.TryGetFloatArray(
                TargetAirBrakeForcesNKey,
                out float[] targetAirBrakeForcesN)) return false;
            if (carIndex >= targetAirBrakeForcesN.Length) return false;

            targetAirBrakeForceN = Math.Max(0f, targetAirBrakeForcesN[carIndex]);
            return true;
        }
    }
}
