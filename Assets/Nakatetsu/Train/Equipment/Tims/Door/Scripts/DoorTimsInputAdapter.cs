using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Speed;
using Nakatetsu.Train.Simulation.Door;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Door
{
    [DisallowMultipleComponent]
    public sealed class DoorTimsInputAdapter : MonoBehaviour, IDoorInputSource
    {
        public bool TryGetCommand(bool leftSide, out DoorMotionCommand command, out int revision)
        {
            command = DoorMotionCommand.Hold;
            revision = 0;
            if (!isActiveAndEnabled) return false;
            var assignment = GetComponent<TrainEquipmentAssignment>();
            var root = GetComponentInParent<TrainRoot>();
            var communication = root != null ? root.GetComponentInChildren<TimsCommunicationController>() : null;
            if (assignment == null || !assignment.isActiveAndEnabled ||
                communication == null || !communication.isActiveAndEnabled ||
                !communication.TryGetComponent<TimsDoorController>(out var doors) || !doors.isActiveAndEnabled ||
                !communication.TryGetLocalBus(assignment.AssignedCarIndex, out TimsBusState bus) ||
                !bus.TryGetInt(leftSide ? TimsDoorController.LeftCommandKey : TimsDoorController.RightCommandKey, out int value) ||
                !bus.TryGetInt(leftSide ? TimsDoorController.LeftCommandRevisionKey : TimsDoorController.RightCommandRevisionKey, out revision))
                return false;
            if (value < (int)DoorMotionCommand.Hold || value > (int)DoorMotionCommand.Close) return false;
            command = (DoorMotionCommand)value;
            return true;
        }

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
