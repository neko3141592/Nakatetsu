using System;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{
    public static class TimsNotchCalculator
    {
        public static void ToBrakeStep(
            int brakeNotch,
            int notchStep,
            int notchStepCount,
            out int brakeStep)
        {
            // 通常ノッチとノッチ内の刻みから連続したブレーキStepへ変換する。
            brakeStep = (brakeNotch - 1) * notchStepCount + notchStep + 1;
        }

        public static void ToBrakeNotchStep(
            int brakeStep,
            int notchStepCount,
            out int brakeNotch,
            out int notchStep)
        {
            // 連続したブレーキStepを通常ノッチとノッチ内の刻みに分解する。
            brakeNotch = ((brakeStep - 1) / notchStepCount) + 1;
            notchStep = (brakeStep - 1) % notchStepCount;
        }

        public static string FormatBrakeNotchStep(int brakeStep, int notchStepCount)
        {
            // 連続したブレーキStepをB1-1形式のNotchStep表記へ変換する。
            if (brakeStep <= 0)
            {
                return "B0-0";
            }

            ToBrakeNotchStep(
                brakeStep,
                Math.Max(1, notchStepCount),
                out int brakeNotch,
                out int notchStep
            );

            return $"B{brakeNotch}-{notchStep}";
        }
    }
}
