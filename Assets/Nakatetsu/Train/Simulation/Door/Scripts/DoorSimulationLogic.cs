using System;

namespace Nakatetsu.Train.Simulation.Door
{
    public enum DoorMotionCommand { Hold, Open, Close }
    public enum DoorStatus { Closed, Opening, Open, Closing, Stopped, Fault }

    [Serializable]
    public sealed class DoorSimulationState
    {
        public float openingRatio;
        public bool closedContact;
        public DoorStatus status;
    }

    public static class DoorSimulationLogic
    {
        public static bool Step(DoorSimulationState state, DoorMotionCommand command,
            float travelTimeSeconds, float deltaTimeSeconds, bool fault)
        {
            if (state == null) return false;
            bool valid = IsFinite(travelTimeSeconds) && travelTimeSeconds > 0f &&
                IsFinite(deltaTimeSeconds) && deltaTimeSeconds >= 0f &&
                IsFinite(state.openingRatio) && state.openingRatio >= 0f && state.openingRatio <= 1f;
            if (!valid || fault)
            {
                state.closedContact = false;
                state.status = DoorStatus.Fault;
                return valid;
            }
            if (command == DoorMotionCommand.Open)
                state.openingRatio = Math.Min(1f, state.openingRatio + deltaTimeSeconds / travelTimeSeconds);
            else if (command == DoorMotionCommand.Close)
                state.openingRatio = Math.Max(0f, state.openingRatio - deltaTimeSeconds / travelTimeSeconds);
            // 浮動小数点の端点残差だけを丸める。途中の位置を閉扱いにはしない。
            if (state.openingRatio <= 0.000001f) state.openingRatio = 0f;
            else if (state.openingRatio >= 0.999999f) state.openingRatio = 1f;
            state.closedContact = state.openingRatio == 0f;
            state.status = state.closedContact ? DoorStatus.Closed : state.openingRatio == 1f ? DoorStatus.Open :
                command == DoorMotionCommand.Open ? DoorStatus.Opening :
                command == DoorMotionCommand.Close ? DoorStatus.Closing : DoorStatus.Stopped;
            return true;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
