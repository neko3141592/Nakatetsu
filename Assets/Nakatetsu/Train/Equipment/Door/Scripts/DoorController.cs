using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Simulation.Door;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Door
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class DoorController : MonoBehaviour, IEquipmentController
    {
        [SerializeField, Min(0f)] private float maximumOpeningSpeedMps = 0.1f;
        [SerializeField] private bool leftOpenPermitted = true;
        [SerializeField] private bool rightOpenPermitted = true;
        private readonly DoorControlContext context = new();
        private IDoorInputSource inputSource;
        private bool leftPending;
        private bool rightPending;
        private DoorMotionCommand leftRequest;
        private DoorMotionCommand rightRequest;
        public DoorMotionCommand LeftCommand => isActiveAndEnabled ? context.leftCommand : DoorMotionCommand.Hold;
        public DoorMotionCommand RightCommand => isActiveAndEnabled ? context.rightCommand : DoorMotionCommand.Hold;
        public bool OpeningInhibited => context.openingInhibited;
        public bool HasOpenCommand => isActiveAndEnabled &&
            ((leftPending ? leftRequest : context.leftCommand) == DoorMotionCommand.Open ||
             (rightPending ? rightRequest : context.rightCommand) == DoorMotionCommand.Open);

        public TrainDoorSimulation Simulation { get; private set; }
        public void SetSimulation(TrainDoorSimulation simulation) => Simulation = simulation;

        public void SetInputSource(IDoorInputSource source) => inputSource = source;
        public void SetOpeningPermissions(bool left, bool right)
        {
            leftOpenPermitted = left;
            rightOpenPermitted = right;
        }
        public void Request(bool leftSide, DoorMotionCommand command)
        {
            if (!isActiveAndEnabled) return;
            if (leftSide) { leftPending = true; leftRequest = command; }
            else { rightPending = true; rightRequest = command; }
        }
        [ContextMenu("Door/Open left")]
        public void OpenLeft() => Request(true, DoorMotionCommand.Open);
        [ContextMenu("Door/Open right")]
        public void OpenRight() => Request(false, DoorMotionCommand.Open);
        [ContextMenu("Door/Close both")]
        public void CloseBoth()
        {
            Request(true, DoorMotionCommand.Close);
            Request(false, DoorMotionCommand.Close);
        }
        public void CollectInput()
        {
            if (inputSource == null)
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                    if (component is IDoorInputSource source) { inputSource = source; break; }
            context.speedMps = 0f;
            context.hasSpeed = isActiveAndEnabled && inputSource != null && inputSource.TryGetSpeedMps(out context.speedMps);
            context.maximumOpeningSpeedMps = maximumOpeningSpeedMps;
            context.leftOpenPermitted = leftOpenPermitted;
            context.rightOpenPermitted = rightOpenPermitted;
            context.hasLeftRequest = leftPending;
            context.hasRightRequest = rightPending;
            context.leftRequest = leftRequest;
            context.rightRequest = rightRequest;
            leftPending = rightPending = false;
        }
        public void Calculate(float deltaTimeSeconds) => DoorControlLogic.Calculate(context);
        public void ApplyOutput(float deltaTimeSeconds) { }
        private void OnDisable()
        {
            leftPending = rightPending = false;
            context.leftCommand = context.rightCommand = DoorMotionCommand.Hold;
        }
    }
}
