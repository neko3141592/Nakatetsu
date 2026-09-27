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
        // 同じ号車・側・戸は、実行順やフレーム数に関係なく同じ開閉時間を使う。
        public static float GetTravelTimeSeconds(float baseTimeSeconds, float variationPercent,
            int carIndex, bool leftSide, int doorIndex)
        {
            if (!IsFinite(baseTimeSeconds) || !IsFinite(variationPercent)) return float.NaN;
            if (variationPercent <= 0f) return baseTimeSeconds;
            float range = Math.Min(variationPercent, 20f) / 100f;
            unchecked
            {
                uint hash = (uint)(carIndex + 1) * 0x9E3779B9u;
                hash ^= (uint)(doorIndex + 1) * 0x85EBCA6Bu;
                hash ^= leftSide ? 0xC2B2AE35u : 0x27D4EB2Fu;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                float offset = (hash / (float)uint.MaxValue) * 2f - 1f;
                return baseTimeSeconds * (1f + offset * range);
            }
        }

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
