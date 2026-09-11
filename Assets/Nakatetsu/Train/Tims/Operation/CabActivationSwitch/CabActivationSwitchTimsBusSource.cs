using UnityEngine;
using Nakatetsu.Train.Equipment;
using Nakatetsu.Train.Operation.CabActivationSwitch;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Integration
{
    [RequireComponent(typeof(CabActivationSwitchController))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class CabActivationSwitchTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        private static readonly TimsTagKey SwitchPositionKey = 
            new ("CabActivationSwitch", "Position");

        [SerializeField] private CabActivationSwitchController cabActivationSwitch;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        public int AssignedCarIndex => ResolveEquipmentAssignment()
            ? equipmentAssignment.AssignedCarIndex
            : -1;

        private void Awake()
        {
            ResolveCabActivationSwitch();
            ResolveEquipmentAssignment();
        }

        private bool ResolveCabActivationSwitch()
        {
            if (cabActivationSwitch == null)
            {
                cabActivationSwitch = GetComponent<CabActivationSwitchController>();
            }
            return cabActivationSwitch != null;
        }

        private bool ResolveEquipmentAssignment()
        {
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return equipmentAssignment != null;
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            if (localBus == null || !ResolveCabActivationSwitch())
            {
                return;
            }

            localBus.SetInt(SwitchPositionKey, (int)cabActivationSwitch.Position);
        }

        
    }
}