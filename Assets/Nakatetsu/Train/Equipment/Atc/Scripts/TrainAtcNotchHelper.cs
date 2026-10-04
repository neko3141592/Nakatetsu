using System;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcNotchHelper
    {
        private static void ToBrakeStep(
            int brakeNotch,
            int notchStep,
            int notchStepCount,
            out int brakeStep)
        {
            // 通常ノッチとノッチ内の刻みから連続したブレーキStepへ変換する。
            brakeStep = (brakeNotch - 1) * notchStepCount + notchStep + 1;
        }

        private static void ToBrakeNotchStep(
            int brakeStep,
            int notchStepCount,
            out int brakeNotch,
            out int notchStep)
        {
            // 連続したブレーキStepを通常ノッチとノッチ内の刻みに分解する。
            brakeNotch = ((brakeStep - 1) / notchStepCount) + 1;
            notchStep = (brakeStep - 1) % notchStepCount;
        }

        private static string FormatBrakeNotchStep(int brakeStep, int notchStepCount)
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

        internal static bool TryGetNearestBrakeStep(
            float decelerationMps2,
            TrainAtcBrakeSettingsInput settings,
            out int brakeStep)
        {
            brakeStep = 0;
            if (settings == null || float.IsNaN(decelerationMps2) || float.IsInfinity(decelerationMps2) ||
                decelerationMps2 < 0f || settings.brakeSubstepCount <= 0 ||
                settings.brakeTargetDecelerationsMps2.Count == 0 || settings.maximumServiceBrakeStep <= 0)
            {
                return false;
            }

            var decelerationsMps2 = settings.brakeTargetDecelerationsMps2;
            long maximumServiceBrakeStep = (long)(decelerationsMps2.Count - 1) *
                settings.brakeSubstepCount + 1;
            if (maximumServiceBrakeStep != settings.maximumServiceBrakeStep)
            {
                return false;
            }
            foreach (float value in decelerationsMps2)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    return false;
                }
            }

            // 緩解の0stepは減速度0。小さい段から比較し、差が同じならその段を維持する。
            double nearestDifferenceMps2 = decelerationMps2;
            for (int stepIndex = 0; stepIndex < settings.maximumServiceBrakeStep; stepIndex++)
            {
                int candidateBrakeStep = stepIndex + 1;
                ToBrakeNotchStep(candidateBrakeStep, settings.brakeSubstepCount,
                    out int brakeNotch, out int notchStep);

                float candidateDecelerationMps2 = decelerationsMps2[brakeNotch - 1];
                if (brakeNotch < decelerationsMps2.Count)
                {
                    // TIMSと同じ式で、通常ノッチ間の設定減速度を刻みに応じて補間する。
                    float decelerationDifferenceMps2 = Math.Max(
                        decelerationsMps2[brakeNotch] - candidateDecelerationMps2, 0f);
                    candidateDecelerationMps2 +=
                        decelerationDifferenceMps2 / settings.brakeSubstepCount * notchStep;
                }

                double differenceMps2 = Math.Abs((double)decelerationMps2 - candidateDecelerationMps2);
                if (differenceMps2 < nearestDifferenceMps2)
                {
                    nearestDifferenceMps2 = differenceMps2;
                    brakeStep = candidateBrakeStep;
                }
            }
            return true;
        }
    }
}
