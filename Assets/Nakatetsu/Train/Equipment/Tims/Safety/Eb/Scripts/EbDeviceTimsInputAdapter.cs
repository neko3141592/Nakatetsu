using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
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

        private void Awake()
        {
            // TIMS通信Controllerと車両割り当ての参照を解決する。
            ResolveReferences();
        }

        public bool TryReadMasterControllerInput(out EbMasterControllerInput input)
        {
            // AssignedCarIndexに対応するLocalBusからマスコン状態を読み取る。
            input = default;
            if (!ResolveReferences() || !equipmentAssignment.IsAssigned ||
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
                isInputEnabled = isInputEnabled
            };
            return true;
        }

        public void Configure(TimsCommunicationController controller)
        {
            // 読み取りに使用するTIMS通信Controllerを設定する。
            communicationController = controller;
        }

        private bool ResolveReferences()
        {
            // 自身の車両割り当てと編成内のTIMS通信Controllerを取得する。
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            if (communicationController == null)
            {
                TrainRoot trainRoot = GetComponentInParent<TrainRoot>(true);
                if (trainRoot != null)
                {
                    communicationController =
                        trainRoot.GetComponentInChildren<TimsCommunicationController>(true);
                }
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
