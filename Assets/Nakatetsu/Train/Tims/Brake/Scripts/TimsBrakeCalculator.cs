using System;
using System.Collections.Generic;
using Nakatetsu.Train.Tims.Internal;
using Nakatetsu.Train.Tims.Notch;

namespace Nakatetsu.Train.Tims.Brake
{
    public static class TimsBrakeCalculator
    {
        public static float GetBrakeDecelerationFromStep(int brakeStep, int subStepCount, List<float> decelerations)
        {
            if (decelerations == null || decelerations.Count == 0)
            {
                return 0f;
            }

            int brakeNotchCount = decelerations.Count;
            int brakeStepCount = (brakeNotchCount - 1) * subStepCount + 1;

            if (brakeStep == 0)
            {
                return 0f;
            }

            if (brakeStep >= brakeStepCount || brakeStep < 0 || subStepCount <= 0)
            {
                return decelerations[brakeNotchCount - 1];
            }

            TimsNotchCalculator.ToSubStepBrakeNotch(brakeStep, subStepCount, out int brakeNotch, out int subStep);

            int currentIndex = brakeNotch - 1;
            int nextIndex = brakeNotch;

            float baseDeceleration = decelerations[currentIndex];
            float interpolatedDeceleration = (decelerations[nextIndex] - decelerations[currentIndex]) / subStepCount * subStep;

            return baseDeceleration + interpolatedDeceleration;
        }

        public static bool TryGetDeceleration(int continuousBrakeNotch, int subStepCount, List<float> decelerations, out float deceleration)
        {
            deceleration = 0f;

            if (continuousBrakeNotch < 0 || subStepCount < 1 || decelerations == null || decelerations.Count == 0)
            {
                return false;
            }

            // B0はブレーキ解除を表すため、有効なステップとして減速度0を返す。
            if (continuousBrakeNotch == 0)
            {
                return true;
            }

            int brakeNotchCount = decelerations.Count;
            int brakeStepCount = (brakeNotchCount - 1) * subStepCount + 1;
            if (continuousBrakeNotch > brakeStepCount)
            {
                return false;
            }

            TimsNotchCalculator.ToSubStepBrakeNotch(
                continuousBrakeNotch,
                subStepCount,
                out int discreteBrakeNotch,
                out int subStep
            );

            // 最終ノッチには次の補間先がないため、テーブル末尾の減速度をそのまま返す。
            if (discreteBrakeNotch == brakeNotchCount)
            {
                deceleration = decelerations[discreteBrakeNotch - 1];
                return true;
            }

            float lower = decelerations[discreteBrakeNotch - 1];
            float upper = decelerations[discreteBrakeNotch];
            deceleration = lower + (upper - lower) / subStepCount * subStep;

            return true;
        }

        public static bool TryGetNearestBrakeStep(float deceleration, int subStepCount, List<float> decelerations, out int brakeStep)
        {
            brakeStep = 0;

            if (deceleration < 0f || subStepCount < 1 || decelerations == null || decelerations.Count == 0)
            {
                return false;
            }

            int brakeNotchCount = decelerations.Count;
            int brakeStepCount = (brakeNotchCount - 1) * subStepCount + 1;

            // B0（ブレーキ解除）も候補に含め、指定された減速度との差が最も小さいステップを探す。
            float nearestDifference = Math.Abs(deceleration);

            for (int currentBrakeStep = 1; currentBrakeStep <= brakeStepCount; currentBrakeStep++)
            {
                float currentDeceleration = GetBrakeDecelerationFromStep(
                    currentBrakeStep,
                    subStepCount,
                    decelerations
                );
                float currentDifference = Math.Abs(deceleration - currentDeceleration);

                // 差が同じ場合は、必要な制動力を下回りにくい強いブレーキ側を選ぶ。
                if (currentDifference < nearestDifference ||
                    TimsMath.Approximately(currentDifference, nearestDifference) && currentBrakeStep > brakeStep)
                {
                    nearestDifference = currentDifference;
                    brakeStep = currentBrakeStep;
                }
            }

            return true;
        }

        public static List<float> AllocateEvenlyWithSaturation(IReadOnlyList<float> caps, float target)
        {
            // caps[i] の範囲で target をなるべく均等に配る（水位均し）
            int count = caps?.Count ?? 0;
            List<float> allocated = new();

            for (int i = 0; i < count; i++)
            {
                allocated.Add(0f);
            }

            if (count == 0 || target <= 0f)
            {
                return allocated;
            }

            bool[] active = new bool[count];
            int activeCount = 0;
            for (int i = 0; i < count; i++)
            {
                float cap = Math.Max(0f, caps[i]);
                if (cap > 0f)
                {
                    active[i] = true;
                    activeCount++;
                }
            }

            float remain = target;
            const float epsilon = 0.0001f;

            int guard = Math.Max(1, count * 4); // 無限ループ防止
            for (int loop = 0; loop < guard && remain > epsilon && activeCount > 0; loop++)
            {
                // 現在飽和していない車両に残りを均等割り
                float share = remain / activeCount;

                bool anyChanged = false;

                for (int i = 0; i < count; i++)
                {
                    // 既に飽和している場合
                    if (!active[i])
                    {
                        continue;
                    }

                    float cap = Math.Max(0f, caps[i]);

                    // 残り
                    float room = cap - allocated[i];

                    if (room <= epsilon)
                    {
                        active[i] = false;
                        activeCount--;
                        continue;
                    }

                    float add = Math.Min(share, room);
                    if (add > 0f)
                    {
                        allocated[i] += add;
                        remain -= add;
                        anyChanged = true;
                    }

                    if (room - add <= epsilon)
                    {
                        active[i] = false;
                        activeCount--;
                    }
                }

                if (!anyChanged)
                {
                    break;
                }
            }

            return allocated;
        }
    }
}
