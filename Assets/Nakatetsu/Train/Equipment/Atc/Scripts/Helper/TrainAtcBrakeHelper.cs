using System;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcBrakeHelper
    {
        internal static bool TryGetNearestBrakeStep(
            float decelerationMps2,
            IReadOnlyList<float> brakeTargetDecelerationsMps2,
            int brakeSubstepCount,
            int maximumServiceBrakeStep,
            out int brakeStep)
        {
            brakeStep = 0;

            // 入力表の取得可否・基本値は工程4で確認する。ここでは計算結果を確認する。
            if (float.IsNaN(decelerationMps2) || float.IsInfinity(decelerationMps2) ||
                decelerationMps2 < 0f)
            {
                return false;
            }

            // 緩解の0stepも候補とする。差が同じ場合は小さい段を選ぶ。
            double nearestDifferenceMps2 = decelerationMps2;
            for (int stepIndex = 0; stepIndex < maximumServiceBrakeStep; stepIndex++)
            {
                int candidateBrakeStep = stepIndex + 1;
                ToBrakeNotchStep(candidateBrakeStep, brakeSubstepCount,
                    out int brakeNotch, out int notchStep);

                float candidateDecelerationMps2 = brakeTargetDecelerationsMps2[brakeNotch - 1];
                if (brakeNotch < brakeTargetDecelerationsMps2.Count)
                {
                    // TIMSと同じ式で、通常ノッチ間の設定減速度を刻みに応じて補間する。
                    float decelerationDifferenceMps2 = Math.Max(
                        brakeTargetDecelerationsMps2[brakeNotch] - candidateDecelerationMps2, 0f);
                    candidateDecelerationMps2 +=
                        decelerationDifferenceMps2 / brakeSubstepCount * notchStep;
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
    }
}
