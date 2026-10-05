using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcPatternLogic
    {
        // 非常側の最高速度・常設制限・ORPに共通で加える速度余裕[km/h]。
        private const float EmergencySpeedMarginKmh = 10f;
        // 下がり切った速度で、この時間分進む距離まで降下・接近区間を延長する[s]。
        private const float DecelerationSectionExtensionSeconds = 1f;
        // 極端に細かい設定で大量のサンプルを確保しないための計算上限。
        private const int MaximumSampleCount = 1000000;

        internal static bool UpdatePattern(TrainAtcContext context)
        {
            var pattern = context.State.pattern;
            var decision = context.State.validation;
            bool isUsable = false;
            if (context.State.protectionMode.isProtectionModeKnown)
            {
                if (decision.result == TrainAtcValidationResult.Adopt)
                {
                    // 生成途中の値はローカルに置き、すべて成功した場合だけ採用する。
                    var createdPattern = new TrainAtcPatternState();
                    if (PreparePath(context, out var path, out float pathLengthM) &&
                        InitializePatterns(context, createdPattern, pathLengthM) &&
                        ApplyPermanentSpeedLimits(createdPattern, path) &&
                        ApplyStopTargets(context, createdPattern) &&
                        CalculateGradientCorrections(context, createdPattern, path, out var corrections) &&
                        IntegratePatterns(context, createdPattern, corrections))
                    {
                        UpdatePatternSections(createdPattern, context.Settings.patternApproachWarningTimeSeconds);
                        AdoptPattern(createdPattern, decision.routeInfomation.atcEdgePath,
                            context.State.operation.currentTravelDirection);
                        if (UpdateCurrentPattern(context, createdPattern, path))
                        {
                            context.State.pattern = createdPattern;
                            isUsable = true;
                        }
                    }
                }
                else if (decision.result == TrainAtcValidationResult.Retain && pattern.isValid)
                {
                    // 無信号の猶予中も、移動後の現在位置から許容速度と区間状態を更新する。
                    if (TrainAtcPatternHelper.TryPreparePath(context.atcEdgesById, pattern.atcEdgePath,
                        pattern.pathStartTravelDirection, false, out var path, out _) &&
                        UpdateCurrentPattern(context, pattern, path))
                    {
                        isUsable = true;
                    }
                }
            }

            // 使用不可・生成失敗・現在位置の参照失敗は、ここでまとめて消去する。
            if (!isUsable)
            {
                ClearPattern(context.State.pattern);
            }
            return isUsable;
        }

        private static bool PreparePath(
            TrainAtcContext context, out List<TrainAtcPatternPathEdge> path, out float pathLengthM)
        {
            path = null;
            pathLengthM = 0f;
            var edgeIds = context.State.validation.routeInfomation?.atcEdgePath;
            if (edgeIds == null || edgeIds.Count == 0 ||
                edgeIds[0] != context.State.operation.currentPosition.atcEdgeId)
            {
                return false;
            }

            // 経路の接続・速度制限・勾配は、生成前に各Edgeを一度だけ確認する。
            return TrainAtcPatternHelper.TryPreparePath(context.atcEdgesById, edgeIds,
                context.State.operation.currentTravelDirection, true, out path, out pathLengthM);
        }

        private static bool InitializePatterns(
            TrainAtcContext context, TrainAtcPatternState pattern, float pathLengthM)
        {
            float requiredSegmentCount = pathLengthM / context.Settings.maximumSamplingIntervalM;
            if (!IsFinite(requiredSegmentCount) || requiredSegmentCount > MaximumSampleCount - 1)
            {
                return false;
            }
            int segmentCount = Mathf.CeilToInt(requiredSegmentCount);
            if (segmentCount <= 0)
            {
                return false;
            }

            int sampleCount = segmentCount + 1;
            pattern.pathLengthM = pathLengthM;
            pattern.samplingIntervalM = pathLengthM / segmentCount;
            float normalSpeedMps = context.Settings.MaximumOperatingSpeedMps;
            float emergencySpeedMps = (context.Settings.maximumOperatingSpeedKmh + EmergencySpeedMarginKmh) / 3.6f;
            if (!IsFinite(normalSpeedMps) || !IsFinite(emergencySpeedMps))
            {
                return false;
            }
            InitializeSamples(pattern.normalPattern, sampleCount, normalSpeedMps,
                context.Settings.serviceDecelerationMps2);
            InitializeSamples(pattern.emergencyPattern, sampleCount, emergencySpeedMps,
                context.Settings.emergencyDecelerationMps2);
            if (context.State.protectionMode.overrunProtectionMode == OverrunProtectionMode.Restricted)
            {
                InitializeSamples(pattern.orpPattern, sampleCount, emergencySpeedMps,
                    context.Settings.orpDecelerationMps2);
            }
            return true;
        }

        private static void InitializeSamples(
            TrainAtcPattern pattern, int sampleCount, float speedMps, float decelerationMps2)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                pattern.samples.Add(new TrainAtcPatternSample
                {
                    allowSpeedMps = speedMps,
                    targetSpeedMps = speedMps,
                    decelerationMps2 = decelerationMps2
                });
            }
        }

        private static bool ApplyPermanentSpeedLimits(
            TrainAtcPatternState pattern, IReadOnlyList<TrainAtcPatternPathEdge> path)
        {
            foreach (var item in path)
            {
                foreach (var section in item.edge.speedLimitSections)
                {
                    float startDistanceM = section.startDistanceOnAtcEdgeM;
                    float endDistanceM = section.endDistanceOnAtcEdgeM;
                    if (item.direction == TrackAtcTravelDirection.BtoA)
                    {
                        startDistanceM = item.edge.lengthM - section.endDistanceOnAtcEdgeM;
                        endDistanceM = item.edge.lengthM - section.startDistanceOnAtcEdgeM;
                    }
                    startDistanceM += item.startDistanceM;
                    endDistanceM += item.startDistanceM;

                    // 短い制限区間も反映するため、境界の前後のサンプルまで広げる。
                    int firstIndex = Mathf.FloorToInt(startDistanceM / pattern.samplingIntervalM);
                    int lastIndex = Mathf.CeilToInt(endDistanceM / pattern.samplingIntervalM);
                    firstIndex = Mathf.Clamp(firstIndex, 0, pattern.normalPattern.samples.Count - 1);
                    lastIndex = Mathf.Clamp(lastIndex, 0, pattern.normalPattern.samples.Count - 1);
                    float normalSpeedMps = section.SpeedLimitMS;
                    float emergencySpeedMps = (section.speedLimitKmh + EmergencySpeedMarginKmh) / 3.6f;
                    if (!IsFinite(emergencySpeedMps))
                    {
                        return false;
                    }
                    for (int i = firstIndex; i <= lastIndex; i++)
                    {
                        LimitSample(pattern.normalPattern.samples[i], normalSpeedMps);
                        LimitSample(pattern.emergencyPattern.samples[i], emergencySpeedMps);
                    }
                }
            }
            return true;
        }

        private static bool ApplyStopTargets(TrainAtcContext context, TrainAtcPatternState pattern)
        {
            var mode = context.State.protectionMode.overrunProtectionMode;
            var settings = context.Settings;
            int stopIndex = pattern.normalPattern.samples.Count - 2;
            if (mode == OverrunProtectionMode.Restricted)
            {
                int firstIndex = GetMarginSampleIndex(pattern, settings.orpMinimumTargetMarginM);
                float normalSpeedMps = settings.orpSpeedLimitKmh / 3.6f;
                float emergencySpeedMps = (settings.orpSpeedLimitKmh + EmergencySpeedMarginKmh) / 3.6f;
                if (!IsFinite(emergencySpeedMps))
                {
                    return false;
                }
                FillSpeedLimit(pattern.normalPattern, firstIndex, normalSpeedMps);
                FillSpeedLimit(pattern.emergencyPattern, firstIndex, emergencySpeedMps);

                // 独立ORPには常設制限を重ねず、指定距離内だけ低速上限と停止目標を置く。
                FillSpeedLimit(pattern.orpPattern, firstIndex, emergencySpeedMps);
                FillSpeedLimit(pattern.orpPattern, stopIndex, 0f);
            }
            else if (mode == OverrunProtectionMode.Normal)
            {
                FillSpeedLimit(pattern.normalPattern, stopIndex, 0f);
            }
            else if (mode == OverrunProtectionMode.None)
            {
                int firstIndex = GetMarginSampleIndex(pattern, settings.serviceStopMarginM);
                FillSpeedLimit(pattern.normalPattern, firstIndex, 0f);
            }
            else
            {
                return false;
            }

            // 通常の非常パターンは全方式で、終端の1サンプル手前から0にする。
            FillSpeedLimit(pattern.emergencyPattern, stopIndex, 0f);
            return true;
        }

        private static int GetMarginSampleIndex(TrainAtcPatternState pattern, float marginM)
        {
            float startDistanceM = Mathf.Max(0f, pattern.pathLengthM - marginM);
            int firstIndex = Mathf.FloorToInt(startDistanceM / pattern.samplingIntervalM);
            return Mathf.Clamp(firstIndex, 0, pattern.normalPattern.samples.Count - 1);
        }

        private static void FillSpeedLimit(TrainAtcPattern pattern, int firstIndex, float speedMps)
        {
            for (int i = firstIndex; i < pattern.samples.Count; i++)
            {
                LimitSample(pattern.samples[i], speedMps);
            }
        }

        private static void LimitSample(TrainAtcPatternSample sample, float speedMps)
        {
            sample.allowSpeedMps = Mathf.Min(sample.allowSpeedMps, speedMps);
        }

        private static bool CalculateGradientCorrections(
            TrainAtcContext context,
            TrainAtcPatternState pattern,
            IReadOnlyList<TrainAtcPatternPathEdge> path,
            out GradientCorrections corrections)
        {
            int segmentCount = pattern.normalPattern.samples.Count - 1;
            corrections = new GradientCorrections
            {
                normalMps2 = new float[segmentCount],
                emergencyMps2 = new float[segmentCount]
            };
            var operation = context.State.operation;
            bool travelsTowardsFront = operation.currentPosition.frontFacesAtoB;
            if (operation.currentTravelDirection == TrackAtcTravelDirection.BtoA)
            {
                travelsTowardsFront = !travelsTowardsFront;
            }

            // 受電器が各サンプルに到達した時の、全車両の中心位置と質量から求める。
            var input = context.Input;
            var validated = context.State.validation;
            for (int i = 0; i < segmentCount; i++)
            {
                float startDistanceM = i * pattern.samplingIntervalM;
                float endDistanceM = Mathf.Min((i + 1) * pattern.samplingIntervalM, pattern.pathLengthM);
                if (!TrainAtcPatternHelper.TryGetTrainGradientCorrection(path, input.cars,
                    pattern.pathLengthM, startDistanceM, endDistanceM,
                    validated.receiverDistanceFromFrontM, travelsTowardsFront, validated.totalMassKg,
                    context.Settings.maximumDownhillGradientPermille,
                    out corrections.normalMps2[i], out corrections.emergencyMps2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IntegratePatterns(
            TrainAtcContext context, TrainAtcPatternState pattern, GradientCorrections corrections)
        {
            // 上限速度を維持しながら、終端側から v² = v0² + 2ax で積分する。
            for (int i = pattern.normalPattern.samples.Count - 2; i >= 0; i--)
            {
                if (!IntegrateSample(pattern.normalPattern.samples, i, pattern.samplingIntervalM,
                    corrections.normalMps2[i]) ||
                    !IntegrateSample(pattern.emergencyPattern.samples, i, pattern.samplingIntervalM,
                    corrections.emergencyMps2[i]))
                {
                    return false;
                }
            }

            if (pattern.orpPattern.samples.Count > 0)
            {
                // ORPの停止曲線は指定距離内だけに積分し、その手前の上限は変更しない。
                int firstIndex = GetMarginSampleIndex(pattern, context.Settings.orpMinimumTargetMarginM);
                for (int i = pattern.orpPattern.samples.Count - 2; i >= firstIndex; i--)
                {
                    if (!IntegrateSample(pattern.orpPattern.samples, i, pattern.samplingIntervalM,
                        corrections.emergencyMps2[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IntegrateSample(
            IReadOnlyList<TrainAtcPatternSample> samples, int index, float intervalM, float gradientCorrectionMps2)
        {
            float decelerationMps2 = samples[index].decelerationMps2 + gradientCorrectionMps2;
            if (!IsFinite(decelerationMps2) || decelerationMps2 <= 0f)
            {
                return false;
            }

            double nextSpeedMps = samples[index + 1].allowSpeedMps;
            float speedMps = (float)Math.Sqrt(nextSpeedMps * nextSpeedMps + 2d * decelerationMps2 * intervalM);
            if (!IsFinite(speedMps))
            {
                return false;
            }
            LimitSample(samples[index], speedMps);
            return true;
        }

        private static void UpdatePatternSections(TrainAtcPatternState pattern, float warningTimeSeconds)
        {
            UpdatePatternSections(pattern.normalPattern, pattern.samplingIntervalM, warningTimeSeconds);
            UpdatePatternSections(pattern.emergencyPattern, pattern.samplingIntervalM, warningTimeSeconds);
            if (pattern.orpPattern.samples.Count > 0)
            {
                UpdatePatternSections(pattern.orpPattern, pattern.samplingIntervalM, warningTimeSeconds);
            }
        }

        private static void UpdatePatternSections(
            TrainAtcPattern pattern, float samplingIntervalM, float warningTimeSeconds)
        {
            var samples = pattern.samples;
            int lastIndex = samples.Count - 1;
            samples[lastIndex].targetSpeedMps = samples[lastIndex].allowSpeedMps;
            float remainingApproachDistanceM = 0f;
            for (int i = lastIndex - 1; i >= 0; i--)
            {
                var sample = samples[i];
                var nextSample = samples[i + 1];
                sample.isDecelerationSection = sample.allowSpeedMps > nextSample.allowSpeedMps;
                if (sample.isDecelerationSection)
                {
                    remainingApproachDistanceM = nextSample.allowSpeedMps * warningTimeSeconds;
                }
                else
                {
                    remainingApproachDistanceM = Mathf.Max(0f, remainingApproachDistanceM - samplingIntervalM);
                }
                sample.isApproachSection = sample.isDecelerationSection || remainingApproachDistanceM > 0f;
                if (sample.isApproachSection)
                {
                    sample.targetSpeedMps = Mathf.Min(sample.allowSpeedMps, nextSample.targetSpeedMps);
                }
                else
                {
                    sample.targetSpeedMps = sample.allowSpeedMps;
                }
            }

            // 本来の降下・予告区間を確定した後、降下終了地点から先へ延長する。
            // 延長したフラグを降下終了の判定に使うと、延長距離が繰り返しリセットされるため、
            // 終了地点はサンプルの速度変化から判定する。
            float remainingExtensionDistanceM = 0f;
            float extensionTargetSpeedMps = 0f;
            for (int i = 1; i <= lastIndex; i++)
            {
                var sample = samples[i];
                bool hasSpeedDecreased = samples[i - 1].allowSpeedMps > sample.allowSpeedMps;
                bool isStillDecreasing = i < lastIndex && sample.allowSpeedMps > samples[i + 1].allowSpeedMps;
                if (hasSpeedDecreased && !isStillDecreasing)
                {
                    extensionTargetSpeedMps = sample.allowSpeedMps;
                    remainingExtensionDistanceM = extensionTargetSpeedMps * DecelerationSectionExtensionSeconds;
                }

                if (remainingExtensionDistanceM > 0f)
                {
                    sample.isDecelerationSection = true;
                    sample.isApproachSection = true;
                    sample.targetSpeedMps = Mathf.Min(sample.targetSpeedMps, extensionTargetSpeedMps);
                    remainingExtensionDistanceM = Mathf.Max(0f, remainingExtensionDistanceM - samplingIntervalM);
                }
            }
        }

        private static void AdoptPattern(
            TrainAtcPatternState pattern, IReadOnlyList<string> edgeIds, TrackAtcTravelDirection direction)
        {
            foreach (string edgeId in edgeIds)
            {
                pattern.atcEdgePath.Add(edgeId);
            }
            pattern.pathStartTravelDirection = direction;
        }

        private static bool UpdateCurrentPattern(
            TrainAtcContext context, TrainAtcPatternState pattern, IReadOnlyList<TrainAtcPatternPathEdge> path)
        {
            var operation = context.State.operation;
            int sampleCount = pattern.normalPattern.samples.Count;
            if (sampleCount < 2 || pattern.emergencyPattern.samples.Count != sampleCount ||
                (pattern.orpPattern.samples.Count != 0 && pattern.orpPattern.samples.Count != sampleCount) ||
                !TrainAtcPatternHelper.TryGetPathDistance(path, operation.currentPosition.atcEdgeId,
                    operation.currentPosition.distanceOnAtcEdgeM, out float distanceOnPathM, out var direction) ||
                direction != operation.currentTravelDirection || distanceOnPathM > pattern.pathLengthM)
            {
                return false;
            }

            int index = Mathf.FloorToInt(distanceOnPathM / pattern.samplingIntervalM);
            index = Mathf.Clamp(index, 0, sampleCount - 2);
            float ratio = Mathf.Clamp01((distanceOnPathM - index * pattern.samplingIntervalM) / pattern.samplingIntervalM);
            UpdateCurrentPattern(pattern.normalPattern, index, ratio);
            UpdateCurrentPattern(pattern.emergencyPattern, index, ratio);
            if (pattern.orpPattern.samples.Count > 0)
            {
                UpdateCurrentPattern(pattern.orpPattern, index, ratio);
            }
            pattern.distanceOnPathM = distanceOnPathM;
            pattern.isValid = true;
            return true;
        }

        private static void UpdateCurrentPattern(TrainAtcPattern pattern, int index, float ratio)
        {
            var start = pattern.samples[index];
            var end = pattern.samples[index + 1];
            pattern.currentAllowSpeedMps = TrainAtcPatternHelper.InterpolateSpeed(start.allowSpeedMps, end.allowSpeedMps, ratio);
            pattern.currentTargetSpeedMps = TrainAtcPatternHelper.InterpolateSpeed(start.targetSpeedMps, end.targetSpeedMps, ratio);
            int currentIndex = index;
            if (ratio >= 1f)
            {
                currentIndex++;
            }
            pattern.currentDecelerationMps2 = pattern.samples[currentIndex].decelerationMps2;
            pattern.isDecelerationSection = pattern.samples[currentIndex].isDecelerationSection;
            pattern.isApproachSection = start.isApproachSection || end.isApproachSection;
        }

        private static void ClearPattern(TrainAtcPatternState pattern)
        {
            pattern.isValid = false;
            pattern.samplingIntervalM = 0f;
            pattern.pathLengthM = 0f;
            pattern.distanceOnPathM = 0f;
            pattern.atcEdgePath.Clear();
            pattern.pathStartTravelDirection = TrackAtcTravelDirection.Unspecified;
            ClearPattern(pattern.normalPattern);
            ClearPattern(pattern.emergencyPattern);
            ClearPattern(pattern.orpPattern);
        }

        private static void ClearPattern(TrainAtcPattern pattern)
        {
            pattern.currentAllowSpeedMps = 0f;
            pattern.currentTargetSpeedMps = 0f;
            pattern.currentDecelerationMps2 = 0f;
            pattern.isDecelerationSection = false;
            pattern.isApproachSection = false;
            pattern.samples.Clear();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private struct GradientCorrections
        {
            internal float[] normalMps2;
            internal float[] emergencyMps2;
        }
    }
}
