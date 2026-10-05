using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcPatternHelper
    {
        private const float GravityMps2 = 9.80665f;

        internal static bool TryPreparePath(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> edgesById,
            IReadOnlyList<string> edgeIds,
            TrackAtcTravelDirection firstDirection,
            bool validateProfiles,
            out List<TrainAtcPatternPathEdge> path,
            out float pathLengthM)
        {
            path = new();
            pathLengthM = 0f;
            if (edgeIds == null || edgeIds.Count == 0 || edgesById == null ||
                (firstDirection != TrackAtcTravelDirection.AtoB && firstDirection != TrackAtcTravelDirection.BtoA))
            {
                return false;
            }

            var usedEdgeIds = new HashSet<string>();
            var direction = firstDirection;
            string entryNodeId = null;
            foreach (string edgeId in edgeIds)
            {
                // 同じEdgeを複数回通る経路は、IDとEdge内距離だけでは位置を特定できない。
                if (string.IsNullOrWhiteSpace(edgeId) || !usedEdgeIds.Add(edgeId) ||
                    !edgesById.TryGetValue(edgeId, out var edge) || edge == null ||
                    edge.atcEdgeId != edgeId || !IsFinite(edge.lengthM) || edge.lengthM <= 0f)
                {
                    return false;
                }
                if (path.Count > 0)
                {
                    bool entersAtNodeA = edge.atcNodeAId == entryNodeId;
                    bool entersAtNodeB = edge.atcNodeBId == entryNodeId;
                    if (string.IsNullOrWhiteSpace(entryNodeId) || entersAtNodeA == entersAtNodeB)
                    {
                        return false;
                    }
                    if (entersAtNodeA)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                }
                if (validateProfiles && !ValidateProfiles(edge))
                {
                    return false;
                }

                path.Add(new TrainAtcPatternPathEdge
                {
                    edge = edge,
                    direction = direction,
                    startDistanceM = pathLengthM
                });
                pathLengthM += edge.lengthM;
                if (!IsFinite(pathLengthM))
                {
                    return false;
                }
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

        internal static bool TryGetPathDistance(
            IReadOnlyList<TrainAtcPatternPathEdge> path,
            string atcEdgeId,
            float distanceOnAtcEdgeM,
            out float distanceOnPathM,
            out TrackAtcTravelDirection direction)
        {
            distanceOnPathM = 0f;
            direction = TrackAtcTravelDirection.Unspecified;
            if (!IsFinite(distanceOnAtcEdgeM) || distanceOnAtcEdgeM < 0f)
            {
                return false;
            }

            foreach (var item in path)
            {
                if (item.edge.atcEdgeId != atcEdgeId)
                {
                    continue;
                }
                if (distanceOnAtcEdgeM > item.edge.lengthM)
                {
                    return false;
                }

                float distanceFromEntryM = distanceOnAtcEdgeM;
                if (item.direction == TrackAtcTravelDirection.BtoA)
                {
                    distanceFromEntryM = item.edge.lengthM - distanceFromEntryM;
                }
                distanceOnPathM = item.startDistanceM + distanceFromEntryM;
                direction = item.direction;
                return true;
            }
            return false;
        }

        internal static bool TryGetTrainGradientCorrection(
            IReadOnlyList<TrainAtcPatternPathEdge> path,
            IReadOnlyList<TrainAtcCarInput> cars,
            float pathLengthM,
            float startDistanceM,
            float endDistanceM,
            float receiverDistanceFromFrontM,
            bool travelsTowardsFront,
            double totalMassKg,
            float maximumDownhillGradientPermille,
            out float normalCorrectionMps2,
            out float emergencyCorrectionMps2)
        {
            normalCorrectionMps2 = 0f;
            emergencyCorrectionMps2 = 0f;
            int leadingCarIndex = 0;
            if (!travelsTowardsFront)
            {
                leadingCarIndex = cars.Count - 1;
            }
            float leadingOffsetM = GetCarOffset(cars[leadingCarIndex].centerDistanceFromFrontM,
                receiverDistanceFromFrontM, travelsTowardsFront);
            float leadingStartM = Mathf.Clamp(startDistanceM + leadingOffsetM, 0f, pathLengthM);
            float leadingEndM = Mathf.Clamp(endDistanceM + leadingOffsetM, 0f, pathLengthM);
            if (!TryGetMinimumGradient(path, leadingStartM, leadingEndM, out float leadingGradientPermille))
            {
                return false;
            }

            double normalMassGradient = 0d;
            double emergencyMassGradient = 0d;
            foreach (var car in cars)
            {
                float offsetM = GetCarOffset(car.centerDistanceFromFrontM,
                    receiverDistanceFromFrontM, travelsTowardsFront);
                float carStartM = startDistanceM + offsetM;
                float carEndM = endDistanceM + offsetM;
                if (!IsFinite(carStartM) || !IsFinite(carEndM))
                {
                    return false;
                }

                float normalGradientPermille = float.PositiveInfinity;
                float emergencyGradientPermille = float.PositiveInfinity;
                if (carStartM < 0f || carEndM > pathLengthM)
                {
                    // 経路外では常用に先頭車の勾配、非常に設定した最大下り勾配を使う。
                    normalGradientPermille = leadingGradientPermille;
                    emergencyGradientPermille = -maximumDownhillGradientPermille;
                }
                if (carEndM >= 0f && carStartM <= pathLengthM)
                {
                    if (!TryGetMinimumGradient(path, Mathf.Max(0f, carStartM),
                        Mathf.Min(pathLengthM, carEndM), out float gradientPermille))
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
            return IsFinite(normalCorrectionMps2) && IsFinite(emergencyCorrectionMps2);
        }

        internal static float InterpolateSpeed(float startSpeedMps, float endSpeedMps, float ratio)
        {
            // v²を補間すると、積分に使った v² = v0² + 2ax と同じ速度になる。
            return (float)Math.Sqrt((1d - ratio) * startSpeedMps * startSpeedMps +
                (double)ratio * endSpeedMps * endSpeedMps);
        }

        private static float GetCarOffset(
            float centerDistanceFromFrontM, float receiverDistanceFromFrontM, bool travelsTowardsFront)
        {
            float offsetM = receiverDistanceFromFrontM - centerDistanceFromFrontM;
            if (!travelsTowardsFront)
            {
                offsetM = -offsetM;
            }
            return offsetM;
        }

        private static bool TryGetMinimumGradient(
            IReadOnlyList<TrainAtcPatternPathEdge> path,
            float startDistanceM,
            float endDistanceM,
            out float gradientPermille)
        {
            gradientPermille = float.PositiveInfinity;
            int firstEdgeIndex = FindPathEdgeIndex(path, startDistanceM);
            // Edge境界では直前と直後の両方の勾配を比較する。
            if (firstEdgeIndex > 0 && startDistanceM == path[firstEdgeIndex].startDistanceM)
            {
                firstEdgeIndex--;
            }

            for (int edgeIndex = firstEdgeIndex; edgeIndex < path.Count; edgeIndex++)
            {
                var item = path[edgeIndex];
                var edge = item.edge;
                float edgeEndM = item.startDistanceM + edge.lengthM;
                if (item.startDistanceM > endDistanceM)
                {
                    break;
                }
                if (edgeEndM < startDistanceM)
                {
                    continue;
                }

                float startOnEdgeM = Mathf.Clamp(startDistanceM - item.startDistanceM, 0f, edge.lengthM);
                float endOnEdgeM = Mathf.Clamp(endDistanceM - item.startDistanceM, 0f, edge.lengthM);
                float gradientSign = 1f;
                if (item.direction == TrackAtcTravelDirection.BtoA)
                {
                    float previousStartM = startOnEdgeM;
                    startOnEdgeM = edge.lengthM - endOnEdgeM;
                    endOnEdgeM = edge.lengthM - previousStartM;
                    gradientSign = -1f;
                }

                float startGradient = GetGradient(edge.gradientProfiles, startOnEdgeM) * gradientSign;
                float endGradient = GetGradient(edge.gradientProfiles, endOnEdgeM) * gradientSign;
                gradientPermille = Mathf.Min(gradientPermille, Mathf.Min(startGradient, endGradient));
                int firstProfileIndex = FindGradientProfileIndex(edge.gradientProfiles, startOnEdgeM);
                for (int i = firstProfileIndex; i < edge.gradientProfiles.Count; i++)
                {
                    var profile = edge.gradientProfiles[i];
                    if (profile.distanceOnAtcEdgeM > endOnEdgeM)
                    {
                        break;
                    }
                    gradientPermille = Mathf.Min(gradientPermille, profile.gradientPermille * gradientSign);
                }
            }
            return IsFinite(gradientPermille);
        }

        private static int FindPathEdgeIndex(IReadOnlyList<TrainAtcPatternPathEdge> path, float distanceM)
        {
            int firstIndex = 0;
            int lastIndex = path.Count - 1;
            while (firstIndex < lastIndex)
            {
                int middleIndex = firstIndex + (lastIndex - firstIndex + 1) / 2;
                if (path[middleIndex].startDistanceM <= distanceM)
                {
                    firstIndex = middleIndex;
                }
                else
                {
                    lastIndex = middleIndex - 1;
                }
            }
            return firstIndex;
        }

        private static float GetGradient(IReadOnlyList<TrackAtcGradientProfile> profiles, float distanceM)
        {
            int nextIndex = Mathf.Max(1, FindGradientProfileIndex(profiles, distanceM));
            var previous = profiles[nextIndex - 1];
            var next = profiles[nextIndex];
            float ratio = (distanceM - previous.distanceOnAtcEdgeM) /
                (next.distanceOnAtcEdgeM - previous.distanceOnAtcEdgeM);
            return Mathf.Lerp(previous.gradientPermille, next.gradientPermille, ratio);
        }

        private static int FindGradientProfileIndex(IReadOnlyList<TrackAtcGradientProfile> profiles, float distanceM)
        {
            int firstIndex = 0;
            int lastIndex = profiles.Count - 1;
            while (firstIndex < lastIndex)
            {
                int middleIndex = firstIndex + (lastIndex - firstIndex) / 2;
                if (profiles[middleIndex].distanceOnAtcEdgeM < distanceM)
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

        private static bool ValidateProfiles(TrackAtcGraphEdge edge)
        {
            var gradients = edge.gradientProfiles;
            if (gradients == null || gradients.Count < 2 || gradients[0] == null ||
                gradients[gradients.Count - 1] == null || gradients[0].distanceOnAtcEdgeM != 0f ||
                gradients[gradients.Count - 1].distanceOnAtcEdgeM != edge.lengthM)
            {
                return false;
            }

            float previousDistanceM = -1f;
            foreach (var profile in gradients)
            {
                if (profile == null || !IsFinite(profile.distanceOnAtcEdgeM) ||
                    profile.distanceOnAtcEdgeM < 0f || profile.distanceOnAtcEdgeM > edge.lengthM ||
                    profile.distanceOnAtcEdgeM <= previousDistanceM || !IsFinite(profile.gradientPermille))
                {
                    return false;
                }
                previousDistanceM = profile.distanceOnAtcEdgeM;
            }
            if (edge.speedLimitSections == null)
            {
                return false;
            }
            foreach (var section in edge.speedLimitSections)
            {
                if (section == null || !IsFinite(section.startDistanceOnAtcEdgeM) ||
                    !IsFinite(section.endDistanceOnAtcEdgeM) || section.startDistanceOnAtcEdgeM < 0f ||
                    section.endDistanceOnAtcEdgeM > edge.lengthM ||
                    section.startDistanceOnAtcEdgeM >= section.endDistanceOnAtcEdgeM ||
                    !IsFinite(section.speedLimitKmh) || section.speedLimitKmh < 0f)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    // 生成時だけ使う、方向と累積距離を確定した経路。採用済みStateとは分ける。
    internal sealed class TrainAtcPatternPathEdge
    {
        internal TrackAtcGraphEdge edge;
        internal TrackAtcTravelDirection direction;
        internal float startDistanceM;
    }
}
