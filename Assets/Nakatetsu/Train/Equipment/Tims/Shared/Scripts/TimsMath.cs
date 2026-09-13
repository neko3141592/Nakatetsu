using System;

namespace Nakatetsu.Train.Equipment.Tims.Internal
{
    internal static class TimsMath
    {
        public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
    }
}
