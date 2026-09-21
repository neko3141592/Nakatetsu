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
        public DoorMotionCommand LeftCommand => isActiveAndEnabled ? context.leftCommand : DoorMotionCommand.Hold;
        public DoorMotionCommand RightCommand => isActiveAndEnabled ? context.rightCommand : DoorMotionCommand.Hold;
        public bool OpeningInhibited => context.openingInhibited;
        public bool HasOpenCommand => isActiveAndEnabled &&
            (HasOpenCommandOnSide(true) || HasOpenCommandOnSide(false));

        public TrainDoorSimulation Simulation { get; private set; }
        public void SetSimulation(TrainDoorSimulation simulation) => Simulation = simulation;

        public void SetInputSource(IDoorInputSource source)
        {
            if (ReferenceEquals(inputSource, source)) return;
            inputSource = source;
            context.hasLeftRevision = context.hasRightRevision = false;
            context.leftCommand = context.rightCommand = DoorMotionCommand.Hold;
        }
        public void SetOpeningPermissions(bool left, bool right)
        {
            leftOpenPermitted = left;
            rightOpenPermitted = right;
        }
        private void ResolveInputSource()
        {
            if (inputSource == null || inputSource is Object unityObject && unityObject == null)
            {
                inputSource = null;
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                    if (component is IDoorInputSource source) { inputSource = source; break; }
            }
        }

        private bool TryGetCommand(bool leftSide, out DoorMotionCommand command, out int revision)
        {
            ResolveInputSource();
            command = DoorMotionCommand.Hold;
            revision = 0;
            return isActiveAndEnabled && inputSource != null &&
                inputSource.TryGetCommand(leftSide, out command, out revision);
        }

        private bool HasOpenCommandOnSide(bool leftSide)
        {
            if (!TryGetCommand(leftSide, out DoorMotionCommand command, out int revision)) return false;
            bool hasRevision = leftSide ? context.hasLeftRevision : context.hasRightRevision;
            int previousRevision = leftSide ? context.leftRevision : context.rightRevision;
            // 入力収集前でも未処理の開指令を全閉監視に含める。
            if (!hasRevision || revision != previousRevision) return command == DoorMotionCommand.Open;
            return (leftSide ? context.leftCommand : context.rightCommand) == DoorMotionCommand.Open;
        }

        public void CollectInput()
        {
            ResolveInputSource();
            context.speedMps = 0f;
            context.hasSpeed = isActiveAndEnabled && inputSource != null && inputSource.TryGetSpeedMps(out context.speedMps);
            context.maximumOpeningSpeedMps = maximumOpeningSpeedMps;
            context.leftOpenPermitted = leftOpenPermitted;
            context.rightOpenPermitted = rightOpenPermitted;
            context.hasLeftRequest = TryGetCommand(true, out context.leftRequest, out context.leftRequestRevision);
            context.hasRightRequest = TryGetCommand(false, out context.rightRequest, out context.rightRequestRevision);
        }
        public void Calculate(float deltaTimeSeconds) => DoorControlLogic.Calculate(context);
        public void ApplyOutput(float deltaTimeSeconds) { }
        private void OnDisable()
        {
            context.hasLeftRequest = context.hasRightRequest = false;
            context.leftCommand = context.rightCommand = DoorMotionCommand.Hold;
        }
    }
}
