using UnityEngine;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BrakeControlDevice))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class BrakeControlDeviceTimsAdapter : MonoBehaviour, IBrakeCommandSource
    {
        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private void Awake()
        {
            ResolveReferences();
        }

        public bool TryGetTargetBrakeForceN(out float targetBrakeForceN)
        {
            targetBrakeForceN = 0f;
            if (!ResolveReferences() || !equipmentAssignment.IsAssigned)
            {
                return false;
            }

            if (communicationController.MasterBus.TryGetBool(TimsBrakeController.IsEmergencyKey, out bool emergency) && emergency)
            {
                targetBrakeForceN = GetComponent<BrakeControlDevice>().MaximumBrakeForceN;
                return true;
            }

            if (!communicationController.MasterBus.TryGetFloatArray(
                    TimsBrakeController.TargetAirBrakeForcesNKey,
                    out float[] targetAirBrakeForcesN))
            {
                return false;
            }

            int carIndex = equipmentAssignment.AssignedCarIndex;
            if (carIndex >= targetAirBrakeForcesN.Length)
            {
                return false;
            }

            targetBrakeForceN = targetAirBrakeForcesN[carIndex];
            return targetBrakeForceN >= 0f && !float.IsNaN(targetBrakeForceN) && !float.IsInfinity(targetBrakeForceN);
        }

        private bool ResolveReferences()
        {
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
            ResolveReferences();
        }
    }
}
