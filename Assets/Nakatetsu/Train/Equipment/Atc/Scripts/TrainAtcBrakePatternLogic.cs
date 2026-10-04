using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcBrakePatternLogic
    {
        private const float GravityMps2 = 9.80665f;

        internal static bool TryGetPathPointInformation(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            float distanceOnPathM,
            out TrainAtcPathPointInformation information)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            information = default;
            if (routeInfomation == null || routeInfomation.atcEdgePath == null ||
                routeInfomation.atcEdgePath.Count == 0 || float.IsNaN(distanceOnPathM) ||
                float.IsInfinity(distanceOnPathM) || distanceOnPathM < 0f)
            {
                return false;
            }

            var state = context.State;
            var direction = state.currentTravelDirection;
            if (!state.hasCurrentPosition || routeInfomation.atcEdgePath[0] != state.currentPosition.atcEdgeId ||
                (direction != TrackAtcTravelDirection.AtoB && direction != TrackAtcTravelDirection.BtoA))
            {
                return false;
            }

            float edgeStartDistanceM = 0f;
            string entryNodeId = null;
            for (int edgeIndex = 0; edgeIndex < routeInfomation.atcEdgePath.Count; edgeIndex++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, routeInfomation.atcEdgePath[edgeIndex], out var edge))
                {
                    return false;
                }
                if (edgeIndex > 0)
                {
                    // 前のEdgeの出口Nodeから、今回のEdgeの進行方向を求める。
                    if (string.IsNullOrWhiteSpace(entryNodeId)) return false;
                    bool entersAtNodeA = edge.atcNodeAId == entryNodeId;
                    bool entersAtNodeB = edge.atcNodeBId == entryNodeId;
                    if (entersAtNodeA == entersAtNodeB) return false;
                    if (entersAtNodeA)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                }

                float edgeEndDistanceM = edgeStartDistanceM + edge.lengthM;
                if (float.IsInfinity(edgeEndDistanceM)) return false;
                bool isLastEdge = edgeIndex == routeInfomation.atcEdgePath.Count - 1;
                // Edgeの境界は次のEdge側とし、経路の終端だけは最後のEdgeに含める。
                if (distanceOnPathM < edgeEndDistanceM || (isLastEdge && distanceOnPathM == edgeEndDistanceM))
                {
                    float distanceOnAtcEdgeM = distanceOnPathM - edgeStartDistanceM;
                    if (distanceOnPathM == edgeEndDistanceM)
                    {
                        // 経路長の加算誤差で、終端がEdgeの範囲外にならないようにする。
                        distanceOnAtcEdgeM = edge.lengthM;
                    }
                    if (direction == TrackAtcTravelDirection.BtoA)
                    {
                        distanceOnAtcEdgeM = edge.lengthM - distanceOnAtcEdgeM;
                    }
                    if (!TryGetGradient(edge, distanceOnAtcEdgeM, out float gradientPermille) ||
                        !TryGetPermanentSpeedLimit(context, edge, distanceOnAtcEdgeM, out float speedLimitMps))
                    {
                        return false;
                    }
                    if (direction == TrackAtcTravelDirection.BtoA)
                    {
                        gradientPermille = -gradientPermille;
                    }

                    information = new TrainAtcPathPointInformation
                    {
                        atcEdgeId = edge.atcEdgeId,
                        trackCircuitId = edge.trackCircuitId,
                        distanceOnPathM = distanceOnPathM,
                        distanceOnAtcEdgeM = distanceOnAtcEdgeM,
                        direction = direction,
                        gradientPermille = gradientPermille,
                        speedLimitMps = speedLimitMps
                    };
                    return true;
                }

                edgeStartDistanceM = edgeEndDistanceM;
                if (direction == TrackAtcTravelDirection.AtoB)
                {
                    entryNodeId = edge.atcNodeBId;
                }
                else
                {
                    entryNodeId = edge.atcNodeAId;
                }
            }
            return false;
        }

        internal static bool TryGetPathDistance(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            string atcEdgeId,
            float distanceOnAtcEdgeM,
            out float distanceOnPathM)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            distanceOnPathM = 0f;
            if (routeInfomation == null || routeInfomation.atcEdgePath == null ||
                routeInfomation.atcEdgePath.Count == 0)
            {
                return false;
            }

            var state = context.State;
            var direction = state.currentTravelDirection;
            if (!state.hasCurrentPosition || routeInfomation.atcEdgePath[0] != state.currentPosition.atcEdgeId)
            {
                return false;
            }

            return TryGetPathDistance(context, routeInfomation.atcEdgePath, direction,
                atcEdgeId, distanceOnAtcEdgeM, out distanceOnPathM, out _);
        }

        private static bool TryGetPathDistance(
            TrainAtcContext context,
            List<string> atcEdgePath,
            TrackAtcTravelDirection direction,
            string atcEdgeId,
            float distanceOnAtcEdgeM,
            out float distanceOnPathM,
            out TrackAtcTravelDirection directionOnEdge)
        {
            distanceOnPathM = 0f;
            directionOnEdge = TrackAtcTravelDirection.Unspecified;
            if (atcEdgePath == null || atcEdgePath.Count == 0 || string.IsNullOrWhiteSpace(atcEdgeId) ||
                float.IsNaN(distanceOnAtcEdgeM) || float.IsInfinity(distanceOnAtcEdgeM) || distanceOnAtcEdgeM < 0f ||
                (direction != TrackAtcTravelDirection.AtoB && direction != TrackAtcTravelDirection.BtoA))
            {
                return false;
            }

            int targetEdgeIndex = atcEdgePath.IndexOf(atcEdgeId);
            // 同じEdgeが複数回含まれる場合は、IDと位置だけでは地点を特定できない。
            if (targetEdgeIndex < 0 || atcEdgePath.LastIndexOf(atcEdgeId) != targetEdgeIndex)
            {
                return false;
            }

            float edgeStartDistanceM = 0f;
            string entryNodeId = null;
            for (int edgeIndex = 0; edgeIndex <= targetEdgeIndex; edgeIndex++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, atcEdgePath[edgeIndex], out var edge))
                {
                    return false;
                }
                if (edgeIndex > 0)
                {
                    // 前のEdgeの出口Nodeから、今回のEdgeの進行方向を求める。
                    if (string.IsNullOrWhiteSpace(entryNodeId)) return false;
                    bool entersAtNodeA = edge.atcNodeAId == entryNodeId;
                    bool entersAtNodeB = edge.atcNodeBId == entryNodeId;
                    if (entersAtNodeA == entersAtNodeB) return false;
                    if (entersAtNodeA)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                }

                float edgeEndDistanceM = edgeStartDistanceM + edge.lengthM;
                if (float.IsInfinity(edgeEndDistanceM)) return false;
                if (edgeIndex == targetEdgeIndex)
                {
                    if (distanceOnAtcEdgeM > edge.lengthM) return false;
                    float distanceFromEntryM = distanceOnAtcEdgeM;
                    if (direction == TrackAtcTravelDirection.BtoA)
                    {
                        distanceFromEntryM = edge.lengthM - distanceOnAtcEdgeM;
                    }
                    distanceOnPathM = edgeStartDistanceM + distanceFromEntryM;
                    directionOnEdge = direction;
                    return true;
                }

                edgeStartDistanceM = edgeEndDistanceM;
                if (direction == TrackAtcTravelDirection.AtoB)
                {
                    entryNodeId = edge.atcNodeBId;
                }
                else
                {
                    entryNodeId = edge.atcNodeAId;
                }
            }
            return false;
        }

        internal static bool TryGetBrakePatternPosition(
            TrainAtcContext context,
            out float distanceOnPathM,
            out int index,
            out float ratio)
        {
            distanceOnPathM = 0f;
            index = 0;
            ratio = 0f;

            var state = context.State;
            if (!state.hasCurrentPosition)
            {
                return false;
            }

            var pattern = state.brakePattern;
            if (pattern.normalPattern.Count < 2 ||
                pattern.emergencyPattern.Count != pattern.normalPattern.Count ||
                float.IsNaN(pattern.samplingIntervalM) || float.IsInfinity(pattern.samplingIntervalM) ||
                pattern.samplingIntervalM <= 0f ||
                !TryGetPathDistance(context, pattern.pathAtcEdges, pattern.pathStartTravelDirection,
                    state.currentPosition.atcEdgeId, state.currentPosition.distanceOnAtcEdgeM,
                    out distanceOnPathM, out var directionOnEdge) || directionOnEdge != state.currentTravelDirection ||
                distanceOnPathM > pattern.pathLengthM)
            {
                return false;
            }

            index = Mathf.FloorToInt(distanceOnPathM / pattern.samplingIntervalM);
            index = Mathf.Clamp(index, 0, pattern.normalPattern.Count - 2);
            ratio = Mathf.Clamp01(
                (distanceOnPathM - index * pattern.samplingIntervalM) / pattern.samplingIntervalM);
            return true;
        }

        internal static void UpdateBrakePattern(TrainAtcContext context, float emergencySpeedMarginKmh)
        {
            var decision = context.State.brakePatternUpdateDecision;
            if (decision.mode == TrainAtcBrakePatternUpdateMode.Retain)
            {
                return;
            }
            if (decision.mode == TrainAtcBrakePatternUpdateMode.Create)
            {
                if (TryCreateBrakePattern(context, decision.routeInfomation,
                    decision.totalMassKg, decision.receiverDistanceFromFrontM, emergencySpeedMarginKmh))
                {
                    return;
                }
            }

            // 消去の判定、または実際の計算が失敗した場合だけ消す。
            ClearBrakePattern(context);
        }

        internal static void UpdateCurrentPattern(
            TrainAtcContext context,
            bool hasValidPattern,
            float distanceOnPathM,
            int index,
            float ratio)
        {
            var pattern = context.State.brakePattern;
            pattern.hasValidPattern = false;
            pattern.distanceOnPathM = 0f;
            pattern.normalAllowSpeedMps = 0f;
            pattern.emergencyAllowSpeedMps = 0f;
            pattern.normalTargetSpeedMps = 0f;
            pattern.emergencyPatternTargetMps = 0f;
            pattern.isNormalDecelerationSection = false;
            pattern.isEmergencyDecelerationSection = false;
            pattern.isNormalPatternApproachSection = false;
            pattern.isEmergencyPatternApproachSection = false;
            if (!hasValidPattern)
            {
                return;
            }

            // 積分した速度の二乗を補間して、現在位置のパターン速度を求める。
            var normalStartSample = pattern.normalPattern[index];
            var normalEndSample = pattern.normalPattern[index + 1];
            var emergencyStartSample = pattern.emergencyPattern[index];
            var emergencyEndSample = pattern.emergencyPattern[index + 1];
            float normalStartMps = normalStartSample.speedLimitMps;
            float normalEndMps = normalEndSample.speedLimitMps;
            float emergencyStartMps = emergencyStartSample.speedLimitMps;
            float emergencyEndMps = emergencyEndSample.speedLimitMps;
            pattern.normalAllowSpeedMps = (float)Math.Sqrt(
                (1d - ratio) * normalStartMps * normalStartMps + (double)ratio * normalEndMps * normalEndMps);
            pattern.emergencyAllowSpeedMps = (float)Math.Sqrt(
                (1d - ratio) * emergencyStartMps * emergencyStartMps + (double)ratio * emergencyEndMps * emergencyEndMps);

            // 区間の始点側のフラグを使う。終端に到達した場合は終端側を使う。
            int currentSampleIndex = index;
            if (ratio >= 1f)
            {
                currentSampleIndex = index + 1;
            }
            pattern.isNormalDecelerationSection = pattern.normalPattern[currentSampleIndex].isDecelerationSection;
            pattern.isEmergencyDecelerationSection = pattern.emergencyPattern[currentSampleIndex].isDecelerationSection;

            // 前後サンプルのどちらかが予告区間なら、現在位置も予告区間として扱う。
            pattern.isNormalPatternApproachSection =
                normalStartSample.isPatternApproachSection || normalEndSample.isPatternApproachSection;
            pattern.isEmergencyPatternApproachSection =
                emergencyStartSample.isPatternApproachSection || emergencyEndSample.isPatternApproachSection;
            float startTargetSpeedMps = normalStartSample.targetSpeedMps;
            float endTargetSpeedMps = normalEndSample.targetSpeedMps;
            pattern.normalTargetSpeedMps = (float)Math.Sqrt(
                (1d - ratio) * startTargetSpeedMps * startTargetSpeedMps +
                (double)ratio * endTargetSpeedMps * endTargetSpeedMps);
            float emergencyStartTargetMps = emergencyStartSample.targetSpeedMps;
            float emergencyEndTargetMps = emergencyEndSample.targetSpeedMps;
            pattern.emergencyPatternTargetMps = (float)Math.Sqrt(
                (1d - ratio) * emergencyStartTargetMps * emergencyStartTargetMps +
                (double)ratio * emergencyEndTargetMps * emergencyEndTargetMps);

            pattern.distanceOnPathM = distanceOnPathM;
            pattern.hasValidPattern = true;
        }

        internal static void ClearBrakePattern(TrainAtcContext context)
        {
            var pattern = context.State.brakePattern;
            pattern.pathStartTravelDirection = TrackAtcTravelDirection.Unspecified;
            pattern.isFrontCab = false;
            pattern.reverserPosition = ReverserPosition.Neutral;
            pattern.overrunProtectionMode = OverrunProtectionMode.None;
            pattern.samplingIntervalM = 0f;
            pattern.pathLengthM = 0f;
            pattern.hasValidPattern = false;
            pattern.distanceOnPathM = 0f;
            pattern.normalAllowSpeedMps = 0f;
            pattern.emergencyAllowSpeedMps = 0f;
            pattern.normalTargetSpeedMps = 0f;
            pattern.emergencyPatternTargetMps = 0f;
            pattern.isNormalDecelerationSection = false;
            pattern.isEmergencyDecelerationSection = false;
            pattern.isNormalPatternApproachSection = false;
            pattern.isEmergencyPatternApproachSection = false;
            pattern.pathAtcEdges.Clear();
            pattern.normalPattern.Clear();
            pattern.emergencyPattern.Clear();
        }

        private static bool TryCreateBrakePattern(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            double totalMassKg,
            float receiverDistanceFromFrontM,
            float emergencySpeedMarginKmh)
        {
            if (!InitializeBrakePattern(context, routeInfomation, emergencySpeedMarginKmh) ||
                !ApplyPermanentSpeedLimits(context, emergencySpeedMarginKmh) ||
                !ApplyOverrunProtection(context, routeInfomation.overrunProtectionMode, emergencySpeedMarginKmh) ||
                !ApplyBrakePattern(context, routeInfomation, totalMassKg, receiverDistanceFromFrontM))
            {
                return false;
            }

            var state = context.State;
            var pattern = context.State.brakePattern;
            pattern.pathStartTravelDirection = state.currentTravelDirection;
            pattern.isFrontCab = state.cab.isFrontCab;
            pattern.reverserPosition = state.cab.reverserPosition;
            pattern.overrunProtectionMode = routeInfomation.overrunProtectionMode;
            float warningTimeSeconds = context.Settings.patternApproachWarningTimeSeconds;
            UpdatePatternSections(pattern.normalPattern, pattern.samplingIntervalM, warningTimeSeconds);
            UpdatePatternSections(pattern.emergencyPattern, pattern.samplingIntervalM, warningTimeSeconds);
            return true;
        }

        private static bool InitializeBrakePattern(
            TrainAtcContext context, TrackCircuitAtcRouteInfomation routeInfomation, float emergencySpeedMarginKmh)
        {
            float maximumSamplingIntervalM = context.Settings.maximumSamplingIntervalM;
            float maxSpeedMps = context.Settings.MaximumOperatingSpeedMps;
            float maxEmergencySpeedMps =
                (context.Settings.maximumOperatingSpeedKmh + emergencySpeedMarginKmh) / 3.6f;

            float pathLengthM = 0f;
            foreach (string atcEdgeId in routeInfomation.atcEdgePath)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, atcEdgeId, out var atcEdge))
                {
                    return false;
                }
                pathLengthM += atcEdge.lengthM;
            }

            if (float.IsInfinity(pathLengthM) || pathLengthM <= 0f)
            {
                return false;
            }
            float requiredSegmentCount = pathLengthM / maximumSamplingIntervalM;
            if (float.IsInfinity(requiredSegmentCount) || requiredSegmentCount >= int.MaxValue)
            {
                return false;
            }
            int segmentCount = Mathf.CeilToInt(requiredSegmentCount);
            if (segmentCount <= 0)
            {
                return false;
            }
            int sampleCount = segmentCount + 1;
            float samplingIntervalM = pathLengthM / segmentCount;

            // 入力を確認した後に、今回のパターンの生成を始める。
            var pattern = context.State.brakePattern;
            pattern.samplingIntervalM = samplingIntervalM;
            pattern.pathLengthM = pathLengthM;
            pattern.pathAtcEdges.Clear();
            pattern.pathAtcEdges.AddRange(routeInfomation.atcEdgePath);
            pattern.normalPattern.Clear();
            pattern.emergencyPattern.Clear();

            for (int i = 0; i < sampleCount; i++)
            {
                pattern.normalPattern.Add(new TrainAtcBrakePatternSample
                {
                    speedLimitMps = maxSpeedMps,
                    targetSpeedMps = maxSpeedMps,
                    decelerationMps2 = context.Settings.serviceDecelerationMps2
                });
                pattern.emergencyPattern.Add(new TrainAtcBrakePatternSample
                {
                    speedLimitMps = maxEmergencySpeedMps,
                    targetSpeedMps = maxEmergencySpeedMps,
                    decelerationMps2 = context.Settings.emergencyDecelerationMps2
                });
            }
            return true;
        }

        private static bool ApplyPermanentSpeedLimits(TrainAtcContext context, float emergencySpeedMarginKmh)
        {
            var pattern = context.State.brakePattern;
            var direction = context.State.currentTravelDirection;
            if (direction != TrackAtcTravelDirection.AtoB && direction != TrackAtcTravelDirection.BtoA)
            {
                return false;
            }
            if (pattern.pathAtcEdges.Count == 0 ||
                pattern.pathAtcEdges[0] != context.State.currentPosition.atcEdgeId)
            {
                return false;
            }

            float distanceOnPathM = 0f;
            string entryNodeId = null;
            for (int edgeIndex = 0; edgeIndex < pattern.pathAtcEdges.Count; edgeIndex++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, pattern.pathAtcEdges[edgeIndex], out var edge))
                {
                    return false;
                }
                if (edgeIndex > 0)
                {
                    // 前のEdgeの出口Nodeから、今回のEdgeの進行方向を求める。
                    if (string.IsNullOrWhiteSpace(entryNodeId)) return false;
                    bool entersAtNodeA = edge.atcNodeAId == entryNodeId;
                    bool entersAtNodeB = edge.atcNodeBId == entryNodeId;
                    if (entersAtNodeA == entersAtNodeB) return false;

                    if (entersAtNodeA)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                }

                if (edge.speedLimitSections == null) return false;
                foreach (var section in edge.speedLimitSections)
                {
                    if (section == null || float.IsNaN(section.startDistanceOnAtcEdgeM) ||
                        float.IsInfinity(section.startDistanceOnAtcEdgeM) ||
                        float.IsNaN(section.endDistanceOnAtcEdgeM) || float.IsInfinity(section.endDistanceOnAtcEdgeM) ||
                        section.startDistanceOnAtcEdgeM < 0f || section.endDistanceOnAtcEdgeM > edge.lengthM ||
                        section.startDistanceOnAtcEdgeM >= section.endDistanceOnAtcEdgeM ||
                        float.IsNaN(section.speedLimitKmh) || float.IsInfinity(section.speedLimitKmh) ||
                        section.speedLimitKmh < 0f)
                    {
                        return false;
                    }

                    float startDistanceM = section.startDistanceOnAtcEdgeM;
                    float endDistanceM = section.endDistanceOnAtcEdgeM;
                    if (direction == TrackAtcTravelDirection.BtoA)
                    {
                        startDistanceM = edge.lengthM - section.endDistanceOnAtcEdgeM;
                        endDistanceM = edge.lengthM - section.startDistanceOnAtcEdgeM;
                    }
                    startDistanceM += distanceOnPathM;
                    endDistanceM += distanceOnPathM;

                    // サンプル間の短い制限も取りこぼさないよう、前後のサンプルまで広げる。
                    int firstSampleIndex = Mathf.FloorToInt(startDistanceM / pattern.samplingIntervalM);
                    int lastSampleIndex = Mathf.CeilToInt(endDistanceM / pattern.samplingIntervalM);
                    firstSampleIndex = Mathf.Clamp(firstSampleIndex, 0, pattern.normalPattern.Count - 1);
                    lastSampleIndex = Mathf.Clamp(lastSampleIndex, 0, pattern.normalPattern.Count - 1);
                    float speedLimitMps = section.SpeedLimitMS;
                    float emergencySpeedLimitMps = (section.speedLimitKmh + emergencySpeedMarginKmh) / 3.6f;
                    for (int sampleIndex = firstSampleIndex; sampleIndex <= lastSampleIndex; sampleIndex++)
                    {
                        ApplySampleSpeedLimit(pattern.normalPattern, sampleIndex, speedLimitMps);
                        ApplySampleSpeedLimit(pattern.emergencyPattern, sampleIndex, emergencySpeedLimitMps);
                    }
                }

                distanceOnPathM += edge.lengthM;
                if (direction == TrackAtcTravelDirection.AtoB)
                {
                    entryNodeId = edge.atcNodeBId;
                }
                else
                {
                    entryNodeId = edge.atcNodeAId;
                }
            }
            return true;
        }

        private static bool ApplyOverrunProtection(
            TrainAtcContext context, OverrunProtectionMode overrunProtectionMode, float emergencySpeedMarginKmh)
        {
            var pattern = context.State.brakePattern;
            if (pattern == null || pattern.normalPattern == null || pattern.emergencyPattern == null ||
                pattern.normalPattern.Count < 2 ||
                pattern.normalPattern.Count != pattern.emergencyPattern.Count ||
                float.IsNaN(pattern.pathLengthM) || float.IsInfinity(pattern.pathLengthM) || pattern.pathLengthM <= 0f ||
                float.IsNaN(pattern.samplingIntervalM) || float.IsInfinity(pattern.samplingIntervalM) ||
                pattern.samplingIntervalM <= 0f)
            {
                return false;
            }

            var settings = context.Settings;
            int stopSampleIndex = pattern.normalPattern.Count - 2;
            if (overrunProtectionMode == OverrunProtectionMode.Restricted)
            {
                float marginM = settings.orpMinimumTargetMarginM;
                float speedLimitKmh = settings.orpSpeedLimitKmh;
                if (float.IsNaN(marginM) || float.IsInfinity(marginM) || marginM < 0f ||
                    float.IsNaN(speedLimitKmh) || float.IsInfinity(speedLimitKmh) || speedLimitKmh < 0f)
                {
                    return false;
                }

                // 指定距離以上手前のサンプルから、ORPの低速制限を反映する。
                float startDistanceM = Mathf.Max(0f, pattern.pathLengthM - marginM);
                int firstSampleIndex = Mathf.FloorToInt(startDistanceM / pattern.samplingIntervalM);
                firstSampleIndex = Mathf.Clamp(firstSampleIndex, 0, pattern.normalPattern.Count - 1);
                float normalSpeedLimitMps = speedLimitKmh / 3.6f;
                float emergencySpeedLimitMps = (speedLimitKmh + emergencySpeedMarginKmh) / 3.6f;
                for (int i = firstSampleIndex; i < pattern.normalPattern.Count; i++)
                {
                    ApplySampleSpeedLimit(pattern.normalPattern, i, normalSpeedLimitMps);
                    ApplySampleSpeedLimit(pattern.emergencyPattern, i, emergencySpeedLimitMps);
                }
            }
            else if (overrunProtectionMode == OverrunProtectionMode.Normal)
            {
                // 過走防護が取れている場合は、常用も停止限界の1サンプル手前から0にする。
                for (int i = stopSampleIndex; i < pattern.normalPattern.Count; i++)
                {
                    ApplySampleSpeedLimit(pattern.normalPattern, i, 0f);
                }
            }
            else if (overrunProtectionMode == OverrunProtectionMode.None)
            {
                float marginM = settings.serviceStopMarginM;
                if (float.IsNaN(marginM) || float.IsInfinity(marginM) || marginM < 0f)
                {
                    return false;
                }

                // 過走防護が取れていない場合は、常用を指定距離手前から0にする。
                float startDistanceM = Mathf.Max(0f, pattern.pathLengthM - marginM);
                int firstSampleIndex = Mathf.FloorToInt(startDistanceM / pattern.samplingIntervalM);
                firstSampleIndex = Mathf.Clamp(firstSampleIndex, 0, pattern.normalPattern.Count - 1);
                for (int i = firstSampleIndex; i < pattern.normalPattern.Count; i++)
                {
                    ApplySampleSpeedLimit(pattern.normalPattern, i, 0f);
                }
            }
            else
            {
                return false;
            }

            // 積分の停止目標として、非常は全モードで停止限界の1サンプル手前から0にする。
            for (int i = stopSampleIndex; i < pattern.emergencyPattern.Count; i++)
            {
                ApplySampleSpeedLimit(pattern.emergencyPattern, i, 0f);
            }
            return true;
        }

        private static bool ApplyBrakePattern(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            double totalMassKg,
            float receiverDistanceFromFrontM)
        {
            var pattern = context.State.brakePattern;
            float serviceDecelerationMps2 = context.Settings.serviceDecelerationMps2;
            float emergencyDecelerationMps2 = context.Settings.emergencyDecelerationMps2;
            float maximumDownhillGradientPermille = context.Settings.maximumDownhillGradientPermille;

            // 勾配データの全件確認は積分前に行い、サンプル・車両ごとには繰り返さない。
            foreach (string atcEdgeId in routeInfomation.atcEdgePath)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, atcEdgeId, out var edge) ||
                    !TryValidateGradientProfiles(edge))
                {
                    return false;
                }
            }

            // 運転台側ではなく、レバーサで指定した編成の前後方向を使う。
            bool travelsTowardsFront = context.State.currentPosition.frontFacesAtoB;
            if (context.State.currentTravelDirection == TrackAtcTravelDirection.BtoA)
            {
                travelsTowardsFront = !travelsTowardsFront;
            }

            // 終端から始点へ積分する。勾配補正は各地点まで編成が移動したと仮定して求める。
            for (int i = pattern.normalPattern.Count - 2; i >= 0; i--)
            {

                float distanceOnPathM = i * pattern.samplingIntervalM;

                float nextDistanceOnPathM = Mathf.Min((i + 1) * pattern.samplingIntervalM, pattern.pathLengthM);

                if (
                    // その地点の勾配による加速度の2乗を求める
                    !TryGetTrainGradientCorrection(
                        context,
                        routeInfomation,
                        distanceOnPathM,
                        nextDistanceOnPathM,
                        receiverDistanceFromFrontM,
                        travelsTowardsFront,
                        totalMassKg,
                        maximumDownhillGradientPermille,
                        out float normalCorrectionMps2,
                        out float emergencyCorrectionMps2
                    )
                )
                {
                    return false;
                }

                float normalDecelerationMps2 = serviceDecelerationMps2 + normalCorrectionMps2;
                float emergencyDecelerationMps2WithGradient = emergencyDecelerationMps2 + emergencyCorrectionMps2;
                if (float.IsNaN(normalDecelerationMps2) || float.IsInfinity(normalDecelerationMps2) ||
                    float.IsNaN(emergencyDecelerationMps2WithGradient) ||
                    float.IsInfinity(emergencyDecelerationMps2WithGradient) ||
                    normalDecelerationMps2 <= 0f || emergencyDecelerationMps2WithGradient <= 0f)
                {
                    return false;
                }

                double nextNormalSpeedMps = pattern.normalPattern[i + 1].speedLimitMps;
                double nextEmergencySpeedMps = pattern.emergencyPattern[i + 1].speedLimitMps;
                float normalSpeedMps = (float)Math.Sqrt(nextNormalSpeedMps * nextNormalSpeedMps +
                    2d * normalDecelerationMps2 * pattern.samplingIntervalM);
                float emergencySpeedMps = (float)Math.Sqrt(nextEmergencySpeedMps * nextEmergencySpeedMps +
                    2d * emergencyDecelerationMps2WithGradient * pattern.samplingIntervalM);
                ApplySampleSpeedLimit(pattern.normalPattern, i, normalSpeedMps);
                ApplySampleSpeedLimit(pattern.emergencyPattern, i, emergencySpeedMps);
            }
            return true;
        }

        private static void ApplySampleSpeedLimit(
            List<TrainAtcBrakePatternSample> samples, int index, float speedLimitMps)
        {
            var sample = samples[index];
            sample.speedLimitMps = Mathf.Min(sample.speedLimitMps, speedLimitMps);
            samples[index] = sample;
        }

        private static void UpdatePatternSections(
            List<TrainAtcBrakePatternSample> samples, float samplingIntervalM, float warningTimeSeconds)
        {
            int lastIndex = samples.Count - 1;
            var lastSample = samples[lastIndex];
            lastSample.targetSpeedMps = lastSample.speedLimitMps;
            lastSample.isDecelerationSection = false;
            lastSample.isPatternApproachSection = false;
            samples[lastIndex] = lastSample;

            float remainingApproachDistanceM = 0f;
            for (int i = lastIndex - 1; i >= 0; i--)
            {
                var sample = samples[i];
                var nextSample = samples[i + 1];
                sample.isDecelerationSection = sample.speedLimitMps > nextSample.speedLimitMps;
                if (sample.isDecelerationSection)
                {
                    // 減速区間のパターン速度で走った場合の、予告時間分の距離。
                    remainingApproachDistanceM = nextSample.speedLimitMps * warningTimeSeconds;
                }
                else
                {
                    remainingApproachDistanceM = Mathf.Max(remainingApproachDistanceM - samplingIntervalM, 0f);
                }

                sample.isPatternApproachSection = sample.isDecelerationSection || remainingApproachDistanceM > 0f;
                if (sample.isPatternApproachSection)
                {
                    // 予告区間も、これから減速する目標速度を引き継ぐ。
                    sample.targetSpeedMps = Mathf.Min(sample.speedLimitMps, nextSample.targetSpeedMps);
                }
                else
                {
                    sample.targetSpeedMps = sample.speedLimitMps;
                }
                samples[i] = sample;
            }
        }

        private static bool TryGetTrainGradientCorrection(
            TrainAtcContext context, TrackCircuitAtcRouteInfomation routeInfomation,
            float distanceOnPathM, float nextDistanceOnPathM,
            float receiverDistanceFromFrontM, bool travelsTowardsFront,
            double totalMassKg, float maximumDownhillGradientPermille,
            out float normalCorrectionMps2, out float emergencyCorrectionMps2)
        {
            normalCorrectionMps2 = 0f;
            emergencyCorrectionMps2 = 0f;
            var cars = context.Input.cars;
            int leadingCarIndex = 0;
            if (!travelsTowardsFront)
            {
                leadingCarIndex = cars.Count - 1;
            }
            float leadingCarOffsetM = receiverDistanceFromFrontM - cars[leadingCarIndex].centerDistanceFromFrontM;
            if (!travelsTowardsFront)
            {
                leadingCarOffsetM = -leadingCarOffsetM;
            }
            float pathLengthM = context.State.brakePattern.pathLengthM;
            // 先頭車両も経路外にある場合は、最寄りの経路端の勾配を代用する。
            float leadingCarDistanceM = Mathf.Clamp(distanceOnPathM + leadingCarOffsetM, 0f, pathLengthM);
            float nextLeadingCarDistanceM = Mathf.Clamp(nextDistanceOnPathM + leadingCarOffsetM, 0f, pathLengthM);
            if (!TryGetPathMinimumGradient(context, routeInfomation,
                leadingCarDistanceM, nextLeadingCarDistanceM, out float leadingGradientPermille))
            {
                return false;
            }

            double normalMassGradient = 0d;
            double emergencyMassGradient = 0d;
            foreach (var car in cars)
            {
                float carOffsetM = receiverDistanceFromFrontM - car.centerDistanceFromFrontM;
                if (!travelsTowardsFront)
                {
                    carOffsetM = -carOffsetM;
                }
                float carDistanceM = distanceOnPathM + carOffsetM;
                float nextCarDistanceM = nextDistanceOnPathM + carOffsetM;
                float normalGradientPermille = float.PositiveInfinity;
                float emergencyGradientPermille = float.PositiveInfinity;
                if (carDistanceM < 0f || nextCarDistanceM > pathLengthM)
                {
                    // 経路外の車両は、常用では先頭勾配、非常では最大の下り勾配を使う。
                    normalGradientPermille = leadingGradientPermille;
                    emergencyGradientPermille = -maximumDownhillGradientPermille;
                }
                if (nextCarDistanceM >= 0f && carDistanceM <= pathLengthM)
                {
                    if (!TryGetPathMinimumGradient(context, routeInfomation,
                        Mathf.Max(0f, carDistanceM), Mathf.Min(pathLengthM, nextCarDistanceM),
                        out float gradientPermille))
                    {
                        return false;
                    }
                    normalGradientPermille = Mathf.Min(normalGradientPermille, gradientPermille);
                    emergencyGradientPermille = Mathf.Min(emergencyGradientPermille, gradientPermille);
                }
                normalMassGradient += (double)car.massKg * normalGradientPermille;
                emergencyMassGradient += (double)car.massKg * emergencyGradientPermille;
            }
            normalCorrectionMps2 = (float)(GravityMps2 * normalMassGradient / (1000d * totalMassKg));
            emergencyCorrectionMps2 = (float)(GravityMps2 * emergencyMassGradient / (1000d * totalMassKg));
            return true;
        }

        private static bool TryGetPathMinimumGradient(
            TrainAtcContext context, TrackCircuitAtcRouteInfomation routeInfomation,
            float startDistanceM, float endDistanceM, out float gradientPermille)
        {
            gradientPermille = float.PositiveInfinity;
            var direction = context.State.currentTravelDirection;
            float edgeStartDistanceM = 0f;
            string entryNodeId = null;
            for (int edgeIndex = 0; edgeIndex < routeInfomation.atcEdgePath.Count; edgeIndex++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, routeInfomation.atcEdgePath[edgeIndex], out var edge))
                {
                    return false;
                }
                if (edgeIndex > 0)
                {
                    bool entersAtNodeA = edge.atcNodeAId == entryNodeId;
                    bool entersAtNodeB = edge.atcNodeBId == entryNodeId;
                    if (string.IsNullOrWhiteSpace(entryNodeId) || entersAtNodeA == entersAtNodeB) return false;
                    if (entersAtNodeA)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                }

                float edgeEndDistanceM = edgeStartDistanceM + edge.lengthM;
                if (startDistanceM <= edgeEndDistanceM && endDistanceM >= edgeStartDistanceM)
                {
                    float startOnEdgeM = Mathf.Clamp(startDistanceM - edgeStartDistanceM, 0f, edge.lengthM);
                    float endOnEdgeM = Mathf.Clamp(endDistanceM - edgeStartDistanceM, 0f, edge.lengthM);
                    if (startDistanceM == edgeEndDistanceM) startOnEdgeM = edge.lengthM;
                    if (endDistanceM == edgeEndDistanceM) endOnEdgeM = edge.lengthM;
                    float gradientSign = 1f;
                    if (direction == TrackAtcTravelDirection.BtoA)
                    {
                        float previousStartM = startOnEdgeM;
                        startOnEdgeM = edge.lengthM - endOnEdgeM;
                        endOnEdgeM = edge.lengthM - previousStartM;
                        gradientSign = -1f;
                    }
                    float startGradient = GetGradient(edge, startOnEdgeM);
                    float endGradient = GetGradient(edge, endOnEdgeM);
                    gradientPermille = Mathf.Min(gradientPermille,
                        Mathf.Min(startGradient * gradientSign, endGradient * gradientSign));
                    // サンプル間の勾配変化点と、Edge境界の両側も比較する。
                    int firstProfileIndex = FindGradientProfileIndex(edge.gradientProfiles, startOnEdgeM);
                    for (int i = firstProfileIndex; i < edge.gradientProfiles.Count; i++)
                    {
                        var profile = edge.gradientProfiles[i];
                        if (profile.distanceOnAtcEdgeM > endOnEdgeM) break;
                        gradientPermille = Mathf.Min(gradientPermille, profile.gradientPermille * gradientSign);
                    }
                }
                if (edgeEndDistanceM > endDistanceM) break;
                edgeStartDistanceM = edgeEndDistanceM;
                if (direction == TrackAtcTravelDirection.AtoB)
                {
                    entryNodeId = edge.atcNodeBId;
                }
                else
                {
                    entryNodeId = edge.atcNodeAId;
                }
            }
            return !float.IsInfinity(gradientPermille);
        }

        private static bool TryGetGradient(
            TrackAtcGraphEdge edge, float distanceOnAtcEdgeM, out float gradientPermille)
        {
            gradientPermille = 0f;
            if (!TryValidateGradientProfiles(edge) || float.IsNaN(distanceOnAtcEdgeM) ||
                float.IsInfinity(distanceOnAtcEdgeM) || distanceOnAtcEdgeM < 0f || distanceOnAtcEdgeM > edge.lengthM)
            {
                return false;
            }
            gradientPermille = GetGradient(edge, distanceOnAtcEdgeM);
            return true;
        }

        private static bool TryValidateGradientProfiles(TrackAtcGraphEdge edge)
        {
            var profiles = edge.gradientProfiles;
            if (profiles == null || profiles.Count < 2) return false;

            // 勾配は両端を含む、Node Aからの距離順のデータを使う。
            float previousDistanceM = -1f;
            foreach (var profile in profiles)
            {
                if (profile == null || float.IsNaN(profile.distanceOnAtcEdgeM) ||
                    float.IsInfinity(profile.distanceOnAtcEdgeM) || profile.distanceOnAtcEdgeM < 0f ||
                    profile.distanceOnAtcEdgeM > edge.lengthM || profile.distanceOnAtcEdgeM <= previousDistanceM ||
                    float.IsNaN(profile.gradientPermille) || float.IsInfinity(profile.gradientPermille))
                {
                    return false;
                }
                previousDistanceM = profile.distanceOnAtcEdgeM;
            }
            if (profiles[0].distanceOnAtcEdgeM != 0f ||
                profiles[profiles.Count - 1].distanceOnAtcEdgeM != edge.lengthM)
            {
                return false;
            }

            return true;
        }

        private static float GetGradient(TrackAtcGraphEdge edge, float distanceOnAtcEdgeM)
        {
            // 確認済みの勾配データから、前後のサンプルを二分探索して線形補間する。
            var profiles = edge.gradientProfiles;
            int nextIndex = Mathf.Max(1, FindGradientProfileIndex(profiles, distanceOnAtcEdgeM));
            var previous = profiles[nextIndex - 1];
            var next = profiles[nextIndex];
            float ratio = (distanceOnAtcEdgeM - previous.distanceOnAtcEdgeM) /
                (next.distanceOnAtcEdgeM - previous.distanceOnAtcEdgeM);
            return Mathf.Lerp(previous.gradientPermille, next.gradientPermille, ratio);
        }

        private static int FindGradientProfileIndex(
            List<TrackAtcGradientProfile> profiles, float distanceOnAtcEdgeM)
        {
            int firstIndex = 0;
            int lastIndex = profiles.Count - 1;
            while (firstIndex < lastIndex)
            {
                int middleIndex = firstIndex + (lastIndex - firstIndex) / 2;
                if (profiles[middleIndex].distanceOnAtcEdgeM < distanceOnAtcEdgeM)
                {
                    firstIndex = middleIndex + 1;
                }
                else
                {
                    lastIndex = middleIndex;
                }
            }
            return firstIndex;
        }

        private static bool TryGetPermanentSpeedLimit(
            TrainAtcContext context, TrackAtcGraphEdge edge, float distanceOnAtcEdgeM, out float speedLimitMps)
        {
            speedLimitMps = context.Settings.MaximumOperatingSpeedMps;
            if (float.IsNaN(speedLimitMps) || float.IsInfinity(speedLimitMps) || speedLimitMps < 0f ||
                edge.speedLimitSections == null)
            {
                return false;
            }
            foreach (var section in edge.speedLimitSections)
            {
                if (section == null || float.IsNaN(section.startDistanceOnAtcEdgeM) ||
                    float.IsInfinity(section.startDistanceOnAtcEdgeM) ||
                    float.IsNaN(section.endDistanceOnAtcEdgeM) || float.IsInfinity(section.endDistanceOnAtcEdgeM) ||
                    section.startDistanceOnAtcEdgeM < 0f || section.endDistanceOnAtcEdgeM > edge.lengthM ||
                    section.startDistanceOnAtcEdgeM >= section.endDistanceOnAtcEdgeM ||
                    float.IsNaN(section.speedLimitKmh) || float.IsInfinity(section.speedLimitKmh) ||
                    section.speedLimitKmh < 0f)
                {
                    return false;
                }
                // 制限区間の両端を含める。共有境界や重複区間では低い方を採用する。
                if (distanceOnAtcEdgeM >= section.startDistanceOnAtcEdgeM &&
                    distanceOnAtcEdgeM <= section.endDistanceOnAtcEdgeM)
                {
                    speedLimitMps = Mathf.Min(speedLimitMps, section.SpeedLimitMS);
                }
            }
            return true;
        }

        private static bool TryGetAtcEdge(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            string atcEdgeId, out TrackAtcGraphEdge edge)
        {
            edge = null;
            if (atcEdgesById == null || string.IsNullOrWhiteSpace(atcEdgeId) ||
                !atcEdgesById.TryGetValue(atcEdgeId, out edge))
            {
                return false;
            }
            return edge != null && edge.atcEdgeId == atcEdgeId &&
                !float.IsNaN(edge.lengthM) && !float.IsInfinity(edge.lengthM) && edge.lengthM > 0f;
        }
    }
}
