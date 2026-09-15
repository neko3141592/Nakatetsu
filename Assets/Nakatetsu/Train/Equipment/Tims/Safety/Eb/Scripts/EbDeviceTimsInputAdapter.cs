using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Equipment.Tims.Speed;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Safety.Eb
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EbDevice))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class EbDeviceTimsInputAdapter : MonoBehaviour, IEbMasterControllerInputSource
    {
        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;
        private TrainRoot trainRoot;

        private void Awake()
        {
            // TIMS通信Controllerと車両割り当ての参照を解決する。
            ResolveReferences();
        }

        public bool TryReadMasterControllerInput(out EbMasterControllerInput input)
        {
            // 有効運転台と速度はMasterBus、自車のマスコン状態はLocalBusから読み取る。
            input = default;
            if (!ResolveReferences() || !equipmentAssignment.IsAssigned ||
                !TryReadActiveCab(out bool isActiveCab) ||
                !communicationController.MasterBus.TryGetFloat(
                    TimsSpeedController.SpeedMpsKey, out float speedMps) ||
                float.IsNaN(speedMps) || float.IsInfinity(speedMps) ||
                !communicationController.TryGetLocalBus(
                    equipmentAssignment.AssignedCarIndex,
                    out TimsBusState localBus) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.PowerPositionKey,
                    out int powerPosition) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.BrakePositionKey,
                    out int brakePosition) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.ReverserPositionKey,
                    out int reverserPosition) ||
                !localBus.TryGetBool(
                    MasterControllerTimsBusSource.IsInputEnabledKey,
                    out bool isInputEnabled) ||
                reverserPosition < (int)ReverserPosition.Reverse ||
                reverserPosition > (int)ReverserPosition.Forward)
            {
                return false;
            }

            input = new EbMasterControllerInput
            {
                powerPosition = powerPosition,
                brakePosition = brakePosition,
                reverserPosition = (ReverserPosition)reverserPosition,
                isInputEnabled = isInputEnabled,
                isActiveCab = isActiveCab,
                speedMps = speedMps
            };
            return true;
        }

        public void Configure(TimsCommunicationController controller)
        {
            // 読み取りに使用するTIMS通信Controllerを設定する。
            communicationController = controller;
        }

        private bool TryReadActiveCab(out bool isActiveCab)
        {
            isActiveCab = false;
            int carCount = trainRoot != null && trainRoot.ConsistDefinition != null
                ? trainRoot.ConsistDefinition.CarCount
                : 0;
            int carIndex = equipmentAssignment.AssignedCarIndex;
            if (carIndex < 0 || carIndex >= carCount ||
                !communicationController.MasterBus.TryGetInt(
                    TimsDirectionController.ActivatedCabPositionKey, out int rawPosition))
            {
                return false;
            }

            switch ((ActivatedCabPosition)rawPosition)
            {
                case ActivatedCabPosition.Front:
                    isActiveCab = carIndex == 0;
                    return true;
                case ActivatedCabPosition.Rear:
                    isActiveCab = carIndex == carCount - 1;
                    return true;
                case ActivatedCabPosition.None:
                    return true;
                default:
                    return false;
            }
        }

        private bool ResolveReferences()
        {
            // 自身の車両割り当てと編成内のTIMS通信Controllerを取得する。
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            if (communicationController == null && trainRoot != null)
            {
                communicationController =
                    trainRoot.GetComponentInChildren<TimsCommunicationController>(true);
            }

            return equipmentAssignment != null && communicationController != null;
        }

        private void OnValidate()
        {
            // Inspector上でも必要な参照を自動解決する。
            ResolveReferences();
        }
    }
}
