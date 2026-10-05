using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainAtcController))]
    public sealed class TrainAtcCabInputAdapter : MonoBehaviour, ITrainAtcCabInputSource
    {
        private TrainRoot trainRoot;
        private TrainCabResolver resolver;

        public bool TryReadCabInput(out TrainAtcCabInput input)
        {
            input = default;
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
                if (trainRoot == null)
                {
                    return false;
                }
                resolver = new TrainCabResolver(trainRoot);
            }

            // Inputと同じ運転台選択を使い、対応するマスコンを直接読む。
            if (!resolver.TryGetActiveMaster(out var master, out var cabPosition))
            {
                return false;
            }

            input = new TrainAtcCabInput
            {
                carIndex = master.AssignedCarIndex,
                isFrontCab = cabPosition == ActivatedCabPosition.Front,
                isKeyInserted = master.IsKeyInserted,
                isInputEnabled = master.IsInputEnabled,
                powerPosition = master.PowerPosition,
                brakePosition = master.BrakePosition,
                serviceBrakePosition = master.ServiceBrakePosition,
                reverserPosition = master.ReverserPosition,
                isNeutral = master.IsNeutral,
                isEmergencyBrake = master.IsEmergencyBrake
            };
            return true;
        }
    }
}
