using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackStationInterlockingLogic
    {
        public static bool TryInitialize(TrackStationInterlockingContext context, TrackStationInterlockingDefinition definition, out string error)
        {
            error = string.Empty;
            if (context.State.routeLock.routes.Count > 0)
            {
                error = "鎖錠を保持している間は再初期化できない。";
                return false;
            }

            var settings = TrackStationInterlockingSettings.CopyFrom(definition);
            if (!TrackStationInterlockingValidationLogic.TryPrepareSettings(settings, out error))
            {
                return false;
            }
            context.Settings = settings;
            TrackStationInterlockingValidationLogic.Initialize(context);
            TrackStationInterlockingPassageLogic.Initialize(context);
            TrackStationInterlockingApproachLockLogic.Initialize(context);
            TrackStationInterlockingRouteLockLogic.Initialize(context);
            TrackStationInterlockingOverrunProtectionLogic.Initialize(context);
            TrackStationInterlockingReservationLogic.Initialize(context);
            TrackStationInterlockingSignalLogic.Initialize(context);
            context.State.isInitialized = true;
            UpdateAfterOperation(context);
            return true;
        }

        public static bool TryRequestRoute(TrackStationInterlockingContext context, string routeId, out string error)
        {
            error = string.Empty;
            if (!context.State.isInitialized)
            {
                error = "連動装置が初期化されていない。";
                return false;
            }

            TrackStationInterlockingValidationLogic.Update(context, 0f);
            if (string.IsNullOrEmpty(routeId) || !context.Settings.routesById.TryGetValue(routeId, out var route))
            {
                error = "進路が定義されていない。";
            }
            else if (context.State.routeLock.routes.ContainsKey(routeId))
            {
                error = "進路は設定済み。";
            }
            else if (!context.State.validation.availableByRouteId[routeId] ||
                !TrackStationInterlockingValidationLogic.HasProtectionInput(context, route))
            {
                error = "進路に必要な入力を取得できない。";
            }
            else if (TrackStationInterlockingReservationLogic.CanReserveMain(context, routeId, out error))
            {
                var mode = route.overrunProtection == null || !route.overrunProtection.isEnabled
                    ? OverrunProtectionMode.None
                    : TrackStationInterlockingReservationLogic.CanReserveProtection(context, routeId)
                        ? OverrunProtectionMode.Normal : OverrunProtectionMode.Restricted;
                TrackStationInterlockingReservationLogic.Register(context, routeId, mode);
                TrackStationInterlockingPassageLogic.Register(context, routeId);
                TrackStationInterlockingApproachLockLogic.Register(context, routeId);
                TrackStationInterlockingRouteLockLogic.Register(context, routeId);
                TrackStationInterlockingOverrunProtectionLogic.Register(context, routeId, mode);
                TrackStationInterlockingSignalLogic.Register(context, routeId);
                UpdateAfterOperation(context);
                return true;
            }
            UpdateAfterOperation(context);
            return false;
        }

        public static bool TryCancelRoute(TrackStationInterlockingContext context, string routeId, out string error)
        {
            error = string.Empty;
            if (!context.State.isInitialized || string.IsNullOrEmpty(routeId) ||
                !context.State.routeLock.routes.TryGetValue(routeId, out var route))
            {
                error = "進路が設定されていない。";
                return false;
            }
            // 同じ取消を繰り返しても、時素と公開値を再更新しない。
            if (route.cancelPending)
            {
                return true;
            }
            if (!context.Input.hasCircuitSource)
            {
                error = "軌道回路の入力を取得できない。";
                UpdateAfterOperation(context);
                return false;
            }

            TrackStationInterlockingRouteLockLogic.RecordCancel(context, routeId);
            TrackStationInterlockingApproachLockLogic.BeginCancel(context, routeId);
            UpdateAfterOperation(context);
            return true;
        }

        public static void Calculate(TrackStationInterlockingContext context, float deltaTimeSeconds)
        {
            if (!context.State.isInitialized)
            {
                return;
            }
            TrackStationInterlockingValidationLogic.Update(context, deltaTimeSeconds);
            if (context.State.validation.hasValidTime)
            {
                TrackStationInterlockingPassageLogic.Update(context);
                TrackStationInterlockingApproachLockLogic.Update(context, deltaTimeSeconds);
                TrackStationInterlockingRouteLockLogic.Update(context);
                TrackStationInterlockingOverrunProtectionLogic.Update(context, deltaTimeSeconds);
                TrackStationInterlockingReservationLogic.Update(context);
                RemoveReleasedRoutes(context);
            }
            TrackStationInterlockingSignalLogic.Update(context, true);
            Publish(context);
        }

        public static bool TryGetRouteStatus(TrackStationInterlockingContext context, string routeId, out TrackStationInterlockingRouteStatus status)
        {
            status = null;
            return context.State.isInitialized && !string.IsNullOrEmpty(routeId) &&
                context.Output.RoutesById.TryGetValue(routeId, out status) && status.IsAvailable;
        }

        public static bool TryGetCircuitPassage(TrackStationInterlockingContext context, string routeId, string circuitId,
            out TrackStationInterlockingCircuitPassageState passage)
        {
            passage = default;
            return !string.IsNullOrEmpty(circuitId) && TryGetRouteStatus(context, routeId, out var status) &&
                status.IsRouteSet && status.CircuitPassageById.TryGetValue(circuitId, out passage);
        }

        public static bool CanRequestTurnoutPosition(TrackStationInterlockingContext context, string routeId, string connectionId)
        {
            if (!context.State.isInitialized || string.IsNullOrEmpty(routeId) || string.IsNullOrEmpty(connectionId) ||
                !context.Settings.routesById.TryGetValue(routeId, out var route))
            {
                return false;
            }
            foreach (var turnout in route.requiredTurnouts)
            {
                if (turnout.connectionId == connectionId)
                {
                    return TrackStationInterlockingSignalLogic.CanRequestTurnoutPosition(context, routeId,
                        connectionId, turnout.requiredPosition);
                }
            }
            if (context.State.overrunProtection.routes.TryGetValue(routeId, out var protection) &&
                protection.mode == OverrunProtectionMode.Normal && protection.IsHeld)
            {
                foreach (var turnout in route.overrunProtection.turnoutRequirements)
                {
                    if (turnout.connectionId == connectionId)
                    {
                        return TrackStationInterlockingSignalLogic.CanRequestTurnoutPosition(context, routeId,
                            connectionId, turnout.requiredPosition);
                    }
                }
            }
            return false;
        }

        private static void UpdateAfterOperation(TrackStationInterlockingContext context)
        {
            // 外部操作は公開値を撤回するだけで、通過・解錠・時素を進めない。
            TrackStationInterlockingValidationLogic.Update(context, 0f);
            TrackStationInterlockingSignalLogic.Update(context, false);
            Publish(context);
        }

        private static void Publish(TrackStationInterlockingContext context)
        {
            bool healthy = context.State.validation.isDefinitionValid && context.State.validation.hasValidTime;
            foreach (bool available in context.State.validation.availableByRouteId.Values)
            {
                healthy &= available;
            }
            context.State.isInterlockingHealthy = healthy;
            context.State.stateRevision++;
            TrackStationInterlockingOutputLogic.UpdateOutput(context);
        }

        private static void RemoveReleasedRoutes(TrackStationInterlockingContext context)
        {
            var finished = new List<string>();
            foreach (var pair in context.State.routeLock.routes)
            {
                if (!pair.Value.isLocked && !context.State.approachLock.routes[pair.Key].isLocked &&
                    !context.State.overrunProtection.routes[pair.Key].IsHeld &&
                    !TrackStationInterlockingReservationLogic.HasReservations(context, pair.Key))
                {
                    finished.Add(pair.Key);
                }
            }
            foreach (string routeId in finished)
            {
                TrackStationInterlockingPassageLogic.Remove(context, routeId);
                TrackStationInterlockingApproachLockLogic.Remove(context, routeId);
                TrackStationInterlockingOverrunProtectionLogic.Remove(context, routeId);
                TrackStationInterlockingSignalLogic.ResetReservation(context, routeId);
                TrackStationInterlockingRouteLockLogic.Remove(context, routeId);
            }
        }
    }
}
