using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcProtectionModeLogic
    {
        internal static void UpdateProtectionMode(TrainAtcContext context)
        {
            var protection = context.State.protectionMode;
            var validation = context.State.validation;

            if (validation.result == TrainAtcValidationResult.Retain)
            {
                // 保持するパターンと同じ方式を使う。方式の写しは他のStateへ置かない。
                return;
            }

            protection.isProtectionModeKnown = false;
            if (validation.result != TrainAtcValidationResult.Adopt)
            {
                return;
            }

            var mode = validation.routeInfomation.overrunProtectionMode;
            if (mode != OverrunProtectionMode.Normal &&
                mode != OverrunProtectionMode.Restricted && mode != OverrunProtectionMode.None)
            {
                return;
            }

            protection.overrunProtectionMode = mode;
            protection.isProtectionModeKnown = true;
        }
    }
}
