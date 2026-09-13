using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Tims.Operation
{
    public static class TimsDirectionLogic
    {
        public static void Calculate(TimsDirectionContext context)
        {
            TimsDirectionInput input = context.Input;
            TimsDirectionOutput output = context.Output;
            output.activatedCabPosition = ActivatedCabPosition.None;
            output.reverserPosition = ReverserPosition.Neutral;
            output.consistDirectionSign = 0;

            if (!input.hasFrontSelection || !input.hasRearSelection)
            {
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
                return;
            }

            if (output.activatedCabPosition == ActivatedCabPosition.Front &&
                input.hasFrontReverserPosition)
            {
                output.reverserPosition = input.frontReverserPosition;
            }
            else if (output.activatedCabPosition == ActivatedCabPosition.Rear &&
                     input.hasRearReverserPosition)
            {
                output.reverserPosition = input.rearReverserPosition;
            }
            else
            {
                return;
            }

            output.consistDirectionSign =
                (int)output.activatedCabPosition * (int)output.reverserPosition;
        }
    }
}
