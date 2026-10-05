using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcValidationHelper
    {
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool IsNonNegativeFinite(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        internal static bool AreSettingsValid(TrainAtcSettings settings)
        {
            // サンプル作成・勾配補正・ブレーキ制御に使う基本設定はここだけで確認する。
            return IsNonNegativeFinite(settings.noSignalTimeoutSeconds) &&
                IsPositiveFinite(settings.maximumSamplingIntervalM) &&
                IsNonNegativeFinite(settings.maximumOperatingSpeedKmh) &&
                IsNonNegativeFinite(settings.patternApproachWarningTimeSeconds) &&
                IsPositiveFinite(settings.serviceDecelerationMps2) &&
                IsPositiveFinite(settings.emergencyDecelerationMps2) &&
                IsNonNegativeFinite(settings.normalBrakeReleaseMarginKmh) &&
                IsPositiveFinite(settings.brakeStepChangeIntervalSeconds) &&
                IsNonNegativeFinite(settings.maximumDownhillGradientPermille) &&
                IsBrakeStepTableValid(settings.brakeStepTable);
        }

        internal static bool AreProtectionSettingsValid(TrainAtcSettings settings, OverrunProtectionMode mode)
        {
            // 方式に固有の設定は、その方式を使う場合だけ確認する。
            if (mode == OverrunProtectionMode.Restricted)
            {
                return IsPositiveFinite(settings.orpDecelerationMps2) &&
                    IsNonNegativeFinite(settings.orpSpeedLimitKmh) &&
                    IsNonNegativeFinite(settings.orpMinimumTargetMarginM);
            }
            if (mode == OverrunProtectionMode.None)
            {
                return IsNonNegativeFinite(settings.serviceStopMarginM);
            }
            return true;
        }

        internal static bool AreBrakeSettingsValid(TrainAtcBrakeSettingsInput settings)
        {
            if (settings == null || settings.brakeSubstepCount <= 0 ||
                settings.maximumServiceBrakeStep <= 0 || settings.brakeTargetDecelerationsMps2.Count == 0)
            {
                return false;
            }

            // B1は1step。その先を通常ノッチ間の刻み数で数える、TIMSと同じ仕様。
            long expectedMaximumStep = (long)(settings.brakeTargetDecelerationsMps2.Count - 1) *
                settings.brakeSubstepCount + 1;
            if (expectedMaximumStep != settings.maximumServiceBrakeStep)
            {
                return false;
            }

            foreach (float decelerationMps2 in settings.brakeTargetDecelerationsMps2)
            {
                if (!IsNonNegativeFinite(decelerationMps2))
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool TryGetTotalMass(IReadOnlyList<TrainAtcCarInput> cars, out double totalMassKg)
        {
            totalMassKg = 0d;
            if (cars == null || cars.Count == 0)
            {
                return false;
            }

            float previousCenterM = -1f;
            foreach (var car in cars)
            {
                if (!IsPositiveFinite(car.massKg) || !IsNonNegativeFinite(car.centerDistanceFromFrontM) ||
                    car.centerDistanceFromFrontM <= previousCenterM)
                {
                    totalMassKg = 0d;
                    return false;
                }

                totalMassKg += car.massKg;
                previousCenterM = car.centerDistanceFromFrontM;
            }
            return true;
        }

        internal static bool IsPositionOnRetainedPath(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> edgesById,
            IReadOnlyList<string> path,
            TrackAtcTravelDirection startDirection,
            string currentEdgeId,
            float distanceOnEdgeM,
            TrackAtcTravelDirection currentDirection)
        {
            if (path == null || path.Count == 0 ||
                (startDirection != TrackAtcTravelDirection.AtoB && startDirection != TrackAtcTravelDirection.BtoA))
            {
                return false;
            }

            string entryNodeId = null;
            var direction = startDirection;
            for (int i = 0; i < path.Count; i++)
            {
                if (string.IsNullOrEmpty(path[i]) || !edgesById.TryGetValue(path[i], out var edge))
                {
                    return false;
                }

                if (i > 0)
                {
                    if (entryNodeId == edge.atcNodeAId)
                    {
                        direction = TrackAtcTravelDirection.AtoB;
                    }
                    else if (entryNodeId == edge.atcNodeBId)
                    {
                        direction = TrackAtcTravelDirection.BtoA;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (edge.atcEdgeId == currentEdgeId)
                {
                    return direction == currentDirection && distanceOnEdgeM >= 0f && distanceOnEdgeM <= edge.lengthM;
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
            return false;
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsBrakeStepTableValid(IReadOnlyList<TrainAtcBrakeStepCondition> table)
        {
            if (table == null || table.Count == 0)
            {
                return false;
            }

            bool hasBaseRow = false;
            for (int i = 0; i < table.Count; i++)
            {
                var row = table[i];
                if (!IsFinite(row.increaseToDeviationKmh) || !IsFinite(row.decreaseToDeviationKmh))
                {
                    return false;
                }

                if (row.brakeStepOffset == 0)
                {
                    hasBaseRow = true;
                }
                if (i == 0)
                {
                    continue;
                }

                // Offsetは昇順なら飛び番も許容する。増段と減段の閾値は別々に保持する。
                var previous = table[i - 1];
                if (row.brakeStepOffset <= previous.brakeStepOffset ||
                    row.increaseToDeviationKmh < previous.increaseToDeviationKmh ||
                    row.decreaseToDeviationKmh < previous.decreaseToDeviationKmh ||
                    previous.decreaseToDeviationKmh >= row.increaseToDeviationKmh)
                {
                    return false;
                }
            }
            return hasBaseRow;
        }
    }
}
