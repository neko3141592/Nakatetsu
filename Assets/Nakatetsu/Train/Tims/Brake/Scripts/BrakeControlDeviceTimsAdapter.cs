using UnityEngine;
using Nakatetsu.Train.Brake.ControlDevice;
using Nakatetsu.Train.Equipment;
using Nakatetsu.Train.Tims.Communication;

namespace Nakatetsu.Train.Tims.Brake
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BrakeControlDevice))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class BrakeControlDeviceTimsAdapter : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private BrakeControlDevice brakeControlDevice;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private void Awake()
        {
            ResolveLocalReferences();
        }

        private void Update()
        {
            ReadAndApply(Time.deltaTime);
        }

        public void Configure(TimsCommunicationController controller)
        {
            communicationController = controller;
            ResolveLocalReferences();
        }

        public bool ReadAndApply(float deltaTimeSeconds)
        {
            if (!ResolveLocalReferences() || communicationController == null)
            {
                return false;
            }

            int carIndex = equipmentAssignment.AssignedCarIndex;
            if (!TimsBrakeBus.TryGetTargetAirBrakeForceN(
                communicationController.MasterBus,
                carIndex,
                out float targetAirBrakeForceN))
            {
                return false;
            }

            brakeControlDevice.Step(targetAirBrakeForceN, deltaTimeSeconds);
            return true;
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
