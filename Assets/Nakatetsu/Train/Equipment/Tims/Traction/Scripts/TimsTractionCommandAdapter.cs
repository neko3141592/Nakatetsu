using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Traction;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class TimsTractionCommandAdapter : MonoBehaviour, ITractionCommandSource
    {
        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private void Awake()
        {
            ResolveReferences();
        }

        public bool TryGetTargetTractionForceN(out float targetTractionForceN)
        {
            targetTractionForceN = 0f;
            if (!ResolveReferences() || !equipmentAssignment.IsAssigned)
            {
                return false;
            }

            if (!communicationController.MasterBus.TryGetFloatArray(
                    TimsTractionController.TargetTractionForcesNKey,
                    out float[] targetTractionForcesN))
            {
                return false;
            }

            int carIndex = equipmentAssignment.AssignedCarIndex;
            if (carIndex >= targetTractionForcesN.Length)
            {
                return false;
            }

            targetTractionForceN = targetTractionForcesN[carIndex];
            return true;
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
