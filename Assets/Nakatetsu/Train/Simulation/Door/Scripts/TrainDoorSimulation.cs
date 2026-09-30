using Nakatetsu.Core.Simulation;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Door
{
    // 車両ごとに1個。配列順は車体前方から後方。左右は編成の基準前方に対して固定。
    [DisallowMultipleComponent]
    public sealed class TrainDoorSimulation : MonoBehaviour, ISimulationController
    {
        [SerializeField, Min(1)] private int doorsPerSide = 4;
        [SerializeField, Min(0.01f)] private float travelTimeSeconds = 3f;
        [SerializeField, Range(0f, 20f)] private float travelTimeVariationPercent = 5f;
        [SerializeField] private bool fault;
        private TrainSimulationAssignment assignment;
        private DoorSimulationState[] left;
        private DoorSimulationState[] right;
        private DoorMotionCommand leftCommand;
        private DoorMotionCommand rightCommand;
        public bool HasValidState { get; private set; }
        public int DoorsPerSide => doorsPerSide;

        public void SetInput(DoorMotionCommand leftValue, DoorMotionCommand rightValue)
        {
            leftCommand = leftValue;
            rightCommand = rightValue;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            HasValidState = false;
            if (!isActiveAndEnabled || doorsPerSide < 1 || doorsPerSide > 16) return;
            if (left == null || left.Length != doorsPerSide)
            {
                left = CreateStates(doorsPerSide);
                right = CreateStates(doorsPerSide);
            }
            if (assignment == null) assignment = GetComponent<TrainSimulationAssignment>();
            int carIndex = assignment != null ? assignment.AssignedCarIndex : -1;
            bool valid = true;
            for (int i = 0; i < doorsPerSide; i++)
            {
                float leftTime = DoorSimulationLogic.GetTravelTimeSeconds(
                    travelTimeSeconds, travelTimeVariationPercent, carIndex, true, i);
                float rightTime = DoorSimulationLogic.GetTravelTimeSeconds(
                    travelTimeSeconds, travelTimeVariationPercent, carIndex, false, i);
                valid &= DoorSimulationLogic.Step(left[i], leftCommand, leftTime, deltaTimeSeconds, fault);
                valid &= DoorSimulationLogic.Step(right[i], rightCommand, rightTime, deltaTimeSeconds, fault);
            }
            HasValidState = valid;
        }

        public bool TryGetDoor(bool leftSide, int index, out float openingRatio, out bool closedContact, out DoorStatus status)
        {
            openingRatio = 0f;
            closedContact = false;
            status = DoorStatus.Fault;
            if (!isActiveAndEnabled || !HasValidState || left == null || right == null ||
                left.Length != doorsPerSide || right.Length != doorsPerSide ||
                index < 0 || index >= doorsPerSide) return false;
            DoorSimulationState state = (leftSide ? left : right)[index];
            openingRatio = state.openingRatio;
            closedContact = state.closedContact;
            status = state.status;
            return true;
        }

        public void ApplyOutput(float deltaTimeSeconds) { }
        private void OnDisable()
        {
            HasValidState = false;
            leftCommand = rightCommand = DoorMotionCommand.Hold;
        }
        private static DoorSimulationState[] CreateStates(int count)
        {
            var states = new DoorSimulationState[count];
            for (int i = 0; i < count; i++) states[i] = new DoorSimulationState();
            return states;
        }
    }
}
