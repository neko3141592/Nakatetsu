using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Door
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsDoorController : MonoBehaviour
    {
        public static readonly TimsTagKey HasValidStateKey = new("Door", "HasValidState");
        public static readonly TimsTagKey AllClosedKey = new("Door", "AllClosed");
        public static readonly TimsTagKey HasFaultKey = new("Door", "HasFault");
        public static readonly TimsTagKey TractionPermittedKey = new("Door", "TractionPermitted");
        public static readonly TimsTagKey CarClosedStatesKey = new("Door", "CarClosedStates");
        // 車両順。-1=未取得、0=未閉、1=全閉。画面側はこの値から制御しない。
        public bool CalculateAndPublish()
        {
            var communication = GetComponent<TimsCommunicationController>();
            var root = GetComponentInParent<TrainRoot>();
            int count = root != null && root.ConsistDefinition != null ? root.ConsistDefinition.CarCount : 0;
            bool valid = isActiveAndEnabled && communication.isActiveAndEnabled && count > 0;
            bool allClosed = valid;
            bool fault = false;
            bool hasOpenCommand = false;
            var states = new int[count];
            for (int i = 0; i < count; i++)
            {
                states[i] = -1;
                if (!communication.TryGetLocalBus(i, out TimsBusState local) ||
                    !local.TryGetBool(DoorTimsBusSource.AllClosedKey, out bool closed) ||
                    !local.TryGetBool(DoorTimsBusSource.HasFaultKey, out bool carFault) ||
                    !local.TryGetBool(DoorTimsBusSource.HasOpenCommandKey, out bool openCommand))
                { valid = false; allClosed = false; continue; }
                states[i] = closed && !carFault ? 1 : 0;
                allClosed &= closed && !carFault;
                fault |= carFault;
                hasOpenCommand |= openCommand;
            }
            var bus = communication.MasterBus;
            bus.SetBool(HasValidStateKey, valid);
            bus.SetBool(AllClosedKey, valid && allClosed);
            bus.SetBool(TractionPermittedKey, valid && allClosed && !hasOpenCommand);
            bus.SetBool(HasFaultKey, fault);
            bus.SetIntArray(CarClosedStatesKey, states);
            return valid && allClosed;
        }
        private void OnDisable()
        {
            var bus = GetComponent<TimsCommunicationController>().MasterBus;
            bus.SetBool(HasValidStateKey, false);
            bus.SetBool(AllClosedKey, false);
            bus.SetBool(TractionPermittedKey, false);
            bus.Remove(HasFaultKey);
            bus.Remove(CarClosedStatesKey);
        }
    }
}
