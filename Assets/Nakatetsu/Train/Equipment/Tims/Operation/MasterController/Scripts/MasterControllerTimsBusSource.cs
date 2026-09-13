using UnityEngine;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;

namespace Nakatetsu.Train.Equipment.Tims.Integration
{
    [RequireComponent(typeof(MasterController))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class MasterControllerTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        private static readonly TimsTagKey PowerPositionKey =
            new("MasterController", "PowerPosition");
        private static readonly TimsTagKey BrakePositionKey =
            new("MasterController", "BrakePosition");
        private static readonly TimsTagKey ServiceBrakePositionKey =
            new("MasterController", "ServiceBrakePosition");
        public static readonly TimsTagKey ReverserPositionKey =
            new("MasterController", "ReverserPosition");
        private static readonly TimsTagKey IsNeutralKey =
            new("MasterController", "IsNeutral");
        private static readonly TimsTagKey IsEmergencyKey =
            new("MasterController", "IsEmergency");
        private static readonly TimsTagKey IsInputEnabledKey =
            new("MasterController", "IsInputEnabled");

        [SerializeField] private MasterController masterController;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        public int AssignedCarIndex => ResolveEquipmentAssignment()
            ? equipmentAssignment.AssignedCarIndex
            : -1;
        public MasterController MasterController => masterController;

        private void Awake()
        {
            ResolveMasterController();
            ResolveEquipmentAssignment();
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            if (localBus == null || !ResolveMasterController())
            {
                return;
            }

            localBus.SetInt(PowerPositionKey, masterController.PowerPosition);
            localBus.SetInt(BrakePositionKey, masterController.BrakePosition);
            localBus.SetInt(ServiceBrakePositionKey, masterController.ServiceBrakePosition);
            localBus.SetInt(ReverserPositionKey, (int)masterController.ReverserPosition);
            localBus.SetBool(IsNeutralKey, masterController.IsNeutral);
            localBus.SetBool(IsEmergencyKey, masterController.IsEmergencyBrake);
            localBus.SetBool(IsInputEnabledKey, masterController.IsInputEnabled);
        }

        private bool ResolveMasterController()
        {
            if (masterController == null)
            {
                masterController = GetComponent<MasterController>();
            }

            return masterController != null;
        }

        private bool ResolveEquipmentAssignment()
        {
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return equipmentAssignment != null;
        }

        private void OnValidate()
        {
            ResolveMasterController();
            ResolveEquipmentAssignment();
        }
    }
}
