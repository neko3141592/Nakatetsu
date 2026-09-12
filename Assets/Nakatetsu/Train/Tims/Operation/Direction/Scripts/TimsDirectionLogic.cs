using Nakatetsu.Train.Operation.CabActivationSwitch;

namespace Nakatetsu.Train.Tims.Operation
{
    public static class TimsDirectionLogic
    {
        public static void Calculate(TimsDirectionContext context)
        {
            TimsDirectionInput input = context.Input;
            TimsDirectionOutput output = context.Output;

            if (!input.hasFrontSelection || !input.hasRearSelection)
            {
                output.activatedCabPosition = ActivatedCabPosition.None;
                return;
            }

            if (input.frontPosition == CabActivationPosition.Front &&
                input.rearPosition == CabActivationPosition.Rear)
            {
                output.activatedCabPosition = ActivatedCabPosition.Front;
            }
            else if (input.frontPosition == CabActivationPosition.Rear &&
                     input.rearPosition == CabActivationPosition.Front)
            {
                output.activatedCabPosition = ActivatedCabPosition.Rear;
            }
            else
            {
                output.activatedCabPosition = ActivatedCabPosition.None;
            }
        }
    }
}
