using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
using UnityEngine;


namespace Nakatetsu.Train.Equipment.Tims.Operation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsDirectionController : MonoBehaviour
    {
        public static readonly TimsTagKey ActivatedCabPositionKey =
            new("Direction", "ActivatedCabPosition");
        public static readonly TimsTagKey ReverserPositionKey =
            new("Direction", "ReverserPosition");
        public static readonly TimsTagKey ConsistDirectionSignKey =
            new("Direction", "ConsistDirectionSign");

        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private TrainRoot trainRoot;

        private readonly TimsDirectionContext context = new();

        public TimsDirectionContext Context => context;
        public TimsDirectionOutput Output => context.Output;

        private void Awake()
        {
            ResolveReferences();
            ConfigureCarIndexes();
        }

        public bool CalculateAndPublish()
        {
            if (!ResolveReferences() || !ConfigureCarIndexes())
            {
                return false;
            }

            ReadInputFromLocalBuses();
            TimsDirectionLogic.Calculate(context);
            communicationController.MasterBus.SetInt(
                ActivatedCabPositionKey,
                (int)context.Output.activatedCabPosition);
            communicationController.MasterBus.SetInt(
                ReverserPositionKey,
                (int)context.Output.reverserPosition);
            communicationController.MasterBus.SetInt(
                ConsistDirectionSignKey,
                context.Output.consistDirectionSign);
            return true;
        }

        private void ReadInputFromLocalBuses()
        {
            TimsDirectionInput input = context.Input;
            input.hasFrontSelection = TryReadPosition(
                input.frontCarIndex,
                out input.frontPosition);
            input.hasRearSelection = TryReadPosition(
                input.rearCarIndex,
                out input.rearPosition);
            input.hasFrontReverserPosition = TryReadReverserPosition(
                input.frontCarIndex,
                out input.frontReverserPosition);
            input.hasRearReverserPosition = TryReadReverserPosition(
                input.rearCarIndex,
                out input.rearReverserPosition);
        }

        private bool TryReadPosition(int carIndex, out CabActivationPosition position)
        {
            position = CabActivationPosition.Off;
            if (!communicationController.TryGetLocalBus(carIndex, out TimsBusState localBus) ||
                !localBus.TryGetInt(
                    CabActivationSwitchTimsBusSource.SwitchPositionKey,
                    out int rawPosition) ||
                rawPosition < (int)CabActivationPosition.Rear ||
                rawPosition > (int)CabActivationPosition.Front)
            {
                return false;
            }

            position = (CabActivationPosition)rawPosition;
            return true;
        }

        private bool TryReadReverserPosition(
            int carIndex,
            out ReverserPosition position)
        {
            position = ReverserPosition.Neutral;
            if (!communicationController.TryGetLocalBus(carIndex, out TimsBusState localBus) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.ReverserPositionKey,
                    out int rawPosition) ||
                rawPosition < (int)ReverserPosition.Reverse ||
                rawPosition > (int)ReverserPosition.Forward)
            {
                return false;
            }

            position = (ReverserPosition)rawPosition;
            return true;
        }

        private bool ConfigureCarIndexes()
        {
            if (trainRoot == null ||
                trainRoot.ConsistDefinition == null ||
                trainRoot.ConsistDefinition.CarCount <= 0)
            {
                return false;
            }

            context.Input.frontCarIndex = 0;
            context.Input.rearCarIndex = trainRoot.ConsistDefinition.CarCount - 1;
            return true;
        }

        private bool ResolveReferences()
        {
            if (communicationController == null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            return communicationController != null && trainRoot != null;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
