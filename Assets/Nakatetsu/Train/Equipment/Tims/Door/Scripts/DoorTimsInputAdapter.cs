using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Speed;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Door
{
    [DisallowMultipleComponent]
    public sealed class DoorTimsInputAdapter : MonoBehaviour, IDoorInputSource
    {
        public bool TryGetSpeedMps(out float speedMps)
        {
            speedMps = 0f;
            if (!isActiveAndEnabled) return false;
            var root = GetComponentInParent<TrainRoot>();
            var communication = root != null ? root.GetComponentInChildren<TimsCommunicationController>() : null;
            return communication != null && communication.isActiveAndEnabled &&
                communication.MasterBus.TryGetBool(TimsSpeedController.HasValidSpeedKey, out bool valid) && valid &&
                communication.MasterBus.TryGetFloat(TimsSpeedController.SpeedMpsKey, out speedMps);
        }
    }
}
