using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Equipment.Tims.Notch;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Traction;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class TimsTractionCommandAdapter : MonoBehaviour, ITractionCommandSource, ITractionDirectionSource
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

            if (communicationController.MasterBus.TryGetBool(TimsBrakeController.IsEmergencyKey, out bool emergency) && emergency)
                return true; // 非常時は明示的に力行0。

            int carIndex = equipmentAssignment.AssignedCarIndex;
            if (carIndex < 0) return false;
            if (communicationController.MasterBus.TryGetInt(
                    TimsNotchController.ResolvedBrakeStepKey, out int brakeStep) && brakeStep > 0)
            {
                if (!communicationController.MasterBus.TryGetFloatArray(
                        TimsBrakeController.TargetRegenForcesNKey, out float[] regenForcesN) ||
                    carIndex >= regenForcesN.Length)
                    return false; // 回生指令欠損時にも力行へ戻さない。

                float regenForceN = regenForcesN[carIndex];
                if (regenForceN < 0f || float.IsNaN(regenForceN) || float.IsInfinity(regenForceN))
                    return false;
                targetTractionForceN = -regenForceN;
                return true;
            }

            if (!communicationController.MasterBus.TryGetFloatArray(
                    TimsTractionController.TargetTractionForcesNKey,
                    out float[] targetTractionForcesN))
            {
                return false;
            }

            if (carIndex >= targetTractionForcesN.Length)
            {
                return false;
            }

            targetTractionForceN = targetTractionForcesN[carIndex];
            return !float.IsNaN(targetTractionForceN) && !float.IsInfinity(targetTractionForceN);
        }

        public bool TryGetTractionDirection(out int directionSign)
        {
            directionSign = 0;
            return ResolveReferences() && communicationController.MasterBus.TryGetInt(
                TimsDirectionController.ConsistDirectionSignKey, out directionSign) &&
                directionSign >= -1 && directionSign <= 1;
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
