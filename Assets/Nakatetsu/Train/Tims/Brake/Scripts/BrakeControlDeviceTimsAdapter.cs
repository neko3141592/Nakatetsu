using UnityEngine;
using Nakatetsu.Train.Brake.ControlDevice;
using Nakatetsu.Train.Equipment;

namespace Nakatetsu.Train.Tims.Brake
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BrakeControlDevice))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class BrakeControlDeviceTimsAdapter : MonoBehaviour
    {
        [SerializeField] private TimsBrakeController timsBrakeController;
        [SerializeField] private BrakeControlDevice brakeControlDevice;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private void Awake()
        {
            ResolveLocalReferences();
        }

        public void Configure(TimsBrakeController controller)
        {
            timsBrakeController = controller;
            ResolveLocalReferences();
        }

        public bool TryReadTargetAirBrakeForceN(out float targetAirBrakeForceN)
        {
            targetAirBrakeForceN = 0f;
            if (!ResolveLocalReferences() || timsBrakeController == null)
            {
                return false;
            }

            int carIndex = equipmentAssignment.AssignedCarIndex;
            return timsBrakeController.TryGetTargetAirBrakeForceN(
                carIndex,
                out targetAirBrakeForceN);
        }

        private bool ResolveLocalReferences()
        {
            if (brakeControlDevice == null)
            {
                brakeControlDevice = GetComponent<BrakeControlDevice>();
            }

            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return brakeControlDevice != null && equipmentAssignment != null;
        }

        private void OnValidate()
        {
            ResolveLocalReferences();
        }
    }
}
