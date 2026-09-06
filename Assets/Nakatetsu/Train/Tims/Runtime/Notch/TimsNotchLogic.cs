using System;

namespace Nakatetsu.Train.Tims.Notch
{
    public static class TimsNotchLogic
    {
        public static void Calculate(TimsNotchContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var input = context.Input;
            var output = context.Output;
            int count = input.cars.Count;
            output.activatedCabIndex = -1;
            if (count > 0 && input.cars[0].hasSelection && input.cars[count - 1].hasSelection)
            {
                var front = input.cars[0].selection;
                var rear = input.cars[count - 1].selection;
                if (front == TimsCabSelection.Forward && rear == TimsCabSelection.Reverse)
                    output.activatedCabIndex = 0;
                else if (front == TimsCabSelection.Reverse && rear == TimsCabSelection.Forward)
                    output.activatedCabIndex = count - 1;
            }
            output.isEmergencyBrakeRequested = output.activatedCabIndex < 0;
            output.manualPowerNotch = 0;
            output.manualBrakeStep = 0;
            output.atcBrakeStep = input.isReady ? Math.Max(0, input.atcBrakeStep) : 0;
            output.reverserPosition = TimsReverserPosition.Neutral;
            int substeps = Math.Max(1, context.Settings.brakeSubstepCount);
            if (input.isReady && output.activatedCabIndex >= 0)
            {
                var cab = input.cars[output.activatedCabIndex];
                output.manualPowerNotch = cab.powerNotch;
                if (cab.brakeNotch > 0)
                {
                    TimsNotchCalculator.ToContinuousBrakeNotch(cab.brakeNotch, 0, substeps, out int step);
                    output.manualBrakeStep = Math.Max(0, step);
                }
                output.reverserPosition = cab.reverserPosition;
            }
            int cabSign = output.activatedCabIndex == 0 ? 1 :
                output.activatedCabIndex >= 0 && output.activatedCabIndex == count - 1 ? -1 : 0;
            int reverserSign = output.reverserPosition == TimsReverserPosition.Forward ? 1 :
                output.reverserPosition == TimsReverserPosition.Reverse ? -1 : 0;
            output.consistForceSign = cabSign * reverserSign;
            output.resolvedBrakeStep = Math.Max(output.manualBrakeStep, output.atcBrakeStep);
            output.resolvedPowerNotch = output.resolvedBrakeStep > 0 ? 0 : output.manualPowerNotch;
            output.manualBrakeStepLabel = TimsNotchCalculator.FormatBrakeStepLabel(output.manualBrakeStep, substeps);
            output.atcBrakeStepLabel = TimsNotchCalculator.FormatBrakeStepLabel(output.atcBrakeStep, substeps);
            output.brakeStepLabel = TimsNotchCalculator.FormatBrakeStepLabel(output.resolvedBrakeStep, substeps);
            output.resolvedNotchLabel = output.resolvedBrakeStep > 0 ? output.brakeStepLabel :
                output.resolvedPowerNotch > 0 ? $"P{output.resolvedPowerNotch}" : "N";
        }
    }
}
