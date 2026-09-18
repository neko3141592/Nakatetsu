using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Simulation.Door;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Door
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DoorController), typeof(TrainEquipmentAssignment))]
    public sealed class DoorTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey LeftStatusKey = new("Door", "LeftStatus");
        public static readonly TimsTagKey RightStatusKey = new("Door", "RightStatus");
        public static readonly TimsTagKey AllClosedKey = new("Door", "AllClosed");
        public static readonly TimsTagKey HasFaultKey = new("Door", "HasFault");
        public static readonly TimsTagKey HasOpenCommandKey = new("Door", "HasOpenCommand");
        private TimsBusState publishedBus;
        private int publishedCarIndex = -1;
        public int AssignedCarIndex
        {
            get
            {
                int index = GetComponent<TrainEquipmentAssignment>().AssignedCarIndex;
                if (index != publishedCarIndex) Clear();
                return index;
            }
        }
        public void WriteTimsBus(TimsBusState localBus)
        {
            if (publishedBus != localBus) Clear();
            publishedBus = localBus;
            publishedCarIndex = AssignedCarIndex;
            var simulation = GetComponent<TrainDoorSimulation>();
            if (!isActiveAndEnabled || !GetComponent<DoorController>().isActiveAndEnabled ||
                publishedCarIndex < 0 || localBus == null || simulation == null || !simulation.HasValidState)
            { Clear(); return; }
            int count = simulation.DoorsPerSide;
            var left = new int[count];
            var right = new int[count];
            bool allClosed = true;
            bool hasFault = false;
            for (int i = 0; i < count; i++)
            {
                if (!simulation.TryGetDoor(true, i, out _, out bool lc, out DoorStatus ls) ||
                    !simulation.TryGetDoor(false, i, out _, out bool rc, out DoorStatus rs))
                { Clear(); return; }
                left[i] = (int)ls; right[i] = (int)rs;
                allClosed &= lc && rc;
                hasFault |= ls == DoorStatus.Fault || rs == DoorStatus.Fault;
            }
            localBus.SetIntArray(LeftStatusKey, left);
            localBus.SetIntArray(RightStatusKey, right);
            localBus.SetBool(AllClosedKey, allClosed);
            localBus.SetBool(HasFaultKey, hasFault);
            localBus.SetBool(HasOpenCommandKey, GetComponent<DoorController>().HasOpenCommand);
        }
        private void Clear()
        {
            publishedBus?.Remove(LeftStatusKey); publishedBus?.Remove(RightStatusKey);
            publishedBus?.Remove(AllClosedKey); publishedBus?.Remove(HasFaultKey);
            publishedBus?.Remove(HasOpenCommandKey);
        }
        private void OnDisable() => Clear();
    }
}
