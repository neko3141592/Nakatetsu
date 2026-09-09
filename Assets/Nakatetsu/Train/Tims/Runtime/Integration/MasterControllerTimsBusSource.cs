using UnityEngine;
using Nakatetsu.Train.Operation;
using Nakatetsu.Train.Equipment;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Integration
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
        private static readonly TimsTagKey ReverserPositionKey =
            new("MasterController", "ReverserPosition");
        private static readonly TimsTagKey IsNeutralKey =
            new("MasterController", "IsNeutral");
        private static readonly TimsTagKey IsEmergencyKey =
            new("MasterController", "IsEmergency");
        private static readonly TimsTagKey IsInputEnabledKey =
            new("MasterController", "IsInputEnabled");

        [SerializeField] private MasterController masterController;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;
        [SerializeField, Min(0.001f)] private float transmissionIntervalSeconds = 0.05f;

        public int AssignedCarIndex => ResolveEquipmentAssignment()
            ? equipmentAssignment.AssignedCarIndex
            : -1;
        public float TransmissionIntervalSeconds => Mathf.Max(0.001f, transmissionIntervalSeconds);
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
            transmissionIntervalSeconds = Mathf.Max(0.001f, transmissionIntervalSeconds);
            ResolveMasterController();
            ResolveEquipmentAssignment();
        }
    }
}
