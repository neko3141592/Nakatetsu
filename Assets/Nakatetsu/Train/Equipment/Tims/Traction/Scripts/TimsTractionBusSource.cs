using UnityEngine;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Traction;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class TimsTractionBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey IsAvailableKey =
            new("Traction", "IsAvailable");
        public static readonly TimsTagKey MotorCountKey =
            new("Traction", "MotorCount");
        public static readonly TimsTagKey RatedPowerWKey =
            new("Traction", "RatedPowerW");
        public static readonly TimsTagKey ActualTractionForceNKey =
            new("Traction", "ActualTractionForceN");

        [SerializeField] private MonoBehaviour tractionEquipmentComponent;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private ITractionEquipment tractionEquipment;

        public int AssignedCarIndex => ResolveReferences()
            ? equipmentAssignment.AssignedCarIndex
            : -1;
        public ITractionEquipment TractionEquipment => ResolveTractionEquipment()
            ? tractionEquipment
            : null;

        private void Awake()
        {
            ResolveReferences();
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            if (localBus == null || !ResolveReferences())
            {
                return;
            }

            localBus.SetBool(IsAvailableKey, tractionEquipment.IsAvailable);
            localBus.SetInt(MotorCountKey, tractionEquipment.MotorCount);
            localBus.SetFloat(RatedPowerWKey, tractionEquipment.RatedPowerW);
            localBus.SetFloat(
                ActualTractionForceNKey,
                tractionEquipment.ActualTractionForceN);
        }

        private bool ResolveReferences()
        {
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return equipmentAssignment != null &&
                   equipmentAssignment.IsAssigned &&
                   ResolveTractionEquipment();
        }

        private bool ResolveTractionEquipment()
        {
            if (tractionEquipmentComponent != null &&
                tractionEquipmentComponent.gameObject == gameObject &&
                tractionEquipmentComponent is ITractionEquipment assignedEquipment)
            {
                tractionEquipment = assignedEquipment;
                return true;
            }

            tractionEquipmentComponent = null;
            tractionEquipment = null;

            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is not ITractionEquipment equipment)
                {
                    continue;
                }

                tractionEquipmentComponent = component;
                tractionEquipment = equipment;
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
