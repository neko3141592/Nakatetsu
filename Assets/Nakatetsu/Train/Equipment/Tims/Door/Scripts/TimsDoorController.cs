using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Simulation.Door;
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
        // LocalBusの指令。左右は運転台ではなく編成前方を基準とする。
        public static readonly TimsTagKey LeftCommandKey = new("Door", "LeftCommand");
        public static readonly TimsTagKey RightCommandKey = new("Door", "RightCommand");
        public static readonly TimsTagKey LeftCommandRevisionKey = new("Door", "LeftCommandRevision");
        public static readonly TimsTagKey RightCommandRevisionKey = new("Door", "RightCommandRevision");
        private int commandRevision;

        // carIndexは0始まり。指令は各車のAdapterが次の入力収集で読み取る。
        public bool Request(int carIndex, bool leftSide, DoorMotionCommand command)
        {
            var communication = GetComponent<TimsCommunicationController>();
            if (!isActiveAndEnabled || !communication.isActiveAndEnabled ||
                !IsValidCommand(command) || !communication.TryGetLocalBus(carIndex, out TimsBusState bus))
                return false;
            PublishCommand(communication, bus, leftSide, command);
            return true;
        }

        public bool RequestAll(bool leftSide, DoorMotionCommand command)
        {
            var communication = GetComponent<TimsCommunicationController>();
            var root = GetComponentInParent<TrainRoot>();
            int count = root != null && root.ConsistDefinition != null ? root.ConsistDefinition.CarCount : 0;
            if (!isActiveAndEnabled || !communication.isActiveAndEnabled || count == 0 || !IsValidCommand(command))
                return false;
            // 途中まで書いて一部の車両だけが動くことを避ける。
            for (int i = 0; i < count; i++)
                if (!communication.TryGetLocalBus(i, out _)) return false;
            for (int i = 0; i < count; i++)
                PublishCommand(communication, communication.GetLocalBus(i), leftSide, command);
            return true;
        }

        [ContextMenu("Door/Open left")]
        public void OpenLeft() => RequestAll(true, DoorMotionCommand.Open);
        [ContextMenu("Door/Open right")]
        public void OpenRight() => RequestAll(false, DoorMotionCommand.Open);
        [ContextMenu("Door/Close both")]
        public void CloseBoth()
        {
            RequestAll(true, DoorMotionCommand.Close);
            RequestAll(false, DoorMotionCommand.Close);
        }

        private static bool IsValidCommand(DoorMotionCommand command) =>
            command == DoorMotionCommand.Hold || command == DoorMotionCommand.Open || command == DoorMotionCommand.Close;

        private void PublishCommand(TimsCommunicationController communication, TimsBusState bus,
            bool leftSide, DoorMotionCommand command)
        {
            bus.SetInt(leftSide ? LeftCommandKey : RightCommandKey, (int)command);
            bus.SetInt(leftSide ? LeftCommandRevisionKey : RightCommandRevisionKey, unchecked(++commandRevision));
            // 接点がまだ閉じている指令受付直後も力行を許可しない。
            if (command == DoorMotionCommand.Open) communication.MasterBus.SetBool(TractionPermittedKey, false);
        }

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
            var communication = GetComponent<TimsCommunicationController>();
            var root = GetComponentInParent<TrainRoot>();
            int count = root != null && root.ConsistDefinition != null ? root.ConsistDefinition.CarCount : 0;
            for (int i = 0; i < count; i++)
            {
                if (!communication.TryGetLocalBus(i, out TimsBusState local)) continue;
                local.Remove(LeftCommandKey);
                local.Remove(RightCommandKey);
                local.Remove(LeftCommandRevisionKey);
                local.Remove(RightCommandRevisionKey);
            }
            var bus = communication.MasterBus;
            bus.SetBool(HasValidStateKey, false);
            bus.SetBool(AllClosedKey, false);
            bus.SetBool(TractionPermittedKey, false);
            bus.Remove(HasFaultKey);
            bus.Remove(CarClosedStatesKey);
        }
    }
}
