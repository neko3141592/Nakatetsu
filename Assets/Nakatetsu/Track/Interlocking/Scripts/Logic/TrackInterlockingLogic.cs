using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackInterlockingLogic
    {
        public static bool TryInitialize(TrackInterlockingContext context, TrackInterlockingDefinition definition, out string error)
        {
            error = string.Empty;
            if (context.State.routeLock.routes.Count > 0)
            {
                error = "鎖錠を保持している間は再初期化できない。";
                return false;
            }

            var settings = TrackInterlockingSettings.CopyFrom(definition);
            if (!TrackInterlockingValidationLogic.TryPrepareSettings(settings, out error))
            {
                return false;
            }
            context.Settings = settings;
            TrackInterlockingValidationLogic.Initialize(context);
            TrackInterlockingPassageLogic.Initialize(context);
            TrackInterlockingApproachLockLogic.Initialize(context);
            TrackInterlockingRouteLockLogic.Initialize(context);
            TrackInterlockingOverrunProtectionLogic.Initialize(context);
            TrackInterlockingReservationLogic.Initialize(context);
            TrackInterlockingSignalLogic.Initialize(context);
            context.State.isInitialized = true;
            UpdateAfterOperation(context);
            return true;
        }

        public static bool TryRequestRoute(TrackInterlockingContext context, string routeId, out string error)
        {
            error = string.Empty;
            if (!context.State.isInitialized)
            {
                error = "連動装置が初期化されていない。";
                return false;
            }

            TrackInterlockingValidationLogic.Update(context, 0f);
            if (string.IsNullOrEmpty(routeId) || !context.Settings.routesById.TryGetValue(routeId, out var route))
            {
                error = "進路が定義されていない。";
            }
            else if (context.State.routeLock.routes.ContainsKey(routeId))
            {
                error = "進路は設定済み。";
            }
            else if (!context.State.validation.availableByRouteId[routeId] ||
                !TrackInterlockingValidationLogic.HasProtectionInput(context, route))
            {
                error = "進路に必要な入力を取得できない。";
            }
            else if (TrackInterlockingReservationLogic.CanReserveMain(context, routeId, out error))
            {
                var mode = route.overrunProtection == null || !route.overrunProtection.isEnabled
                    ? OverrunProtectionMode.None
                    : TrackInterlockingReservationLogic.CanReserveProtection(context, routeId)
                        ? OverrunProtectionMode.Normal : OverrunProtectionMode.Restricted;
                TrackInterlockingReservationLogic.Register(context, routeId, mode);
                TrackInterlockingPassageLogic.Register(context, routeId);
                TrackInterlockingApproachLockLogic.Register(context, routeId);
                TrackInterlockingRouteLockLogic.Register(context, routeId);
                TrackInterlockingOverrunProtectionLogic.Register(context, routeId, mode);
                TrackInterlockingSignalLogic.Register(context, routeId);
                UpdateAfterOperation(context);
                return true;
            }
            UpdateAfterOperation(context);
            return false;
        }

        public static bool TryCancelRoute(TrackInterlockingContext context, string routeId, out string error)
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

            TrackInterlockingRouteLockLogic.RecordCancel(context, routeId);
            TrackInterlockingApproachLockLogic.BeginCancel(context, routeId);
            UpdateAfterOperation(context);
            return true;
        }

        public static void Calculate(TrackInterlockingContext context, float deltaTimeSeconds)
        {
            if (!context.State.isInitialized)
            {
                return;
            }
            TrackInterlockingValidationLogic.Update(context, deltaTimeSeconds);
            if (context.State.validation.hasValidTime)
            {
                TrackInterlockingPassageLogic.Update(context);
                TrackInterlockingApproachLockLogic.Update(context, deltaTimeSeconds);
                TrackInterlockingRouteLockLogic.Update(context);
                TrackInterlockingOverrunProtectionLogic.Update(context, deltaTimeSeconds);
                TrackInterlockingReservationLogic.Update(context);
                RemoveReleasedRoutes(context);
            }
            TrackInterlockingSignalLogic.Update(context, true);
            Publish(context);
        }

        public static bool TryGetRouteStatus(TrackInterlockingContext context, string routeId, out TrackInterlockingRouteStatus status)
        {
            status = null;
            return context.State.isInitialized && !string.IsNullOrEmpty(routeId) &&
                context.Output.RoutesById.TryGetValue(routeId, out status) && status.IsAvailable;
        }

        public static bool TryGetCircuitPassage(TrackInterlockingContext context, string routeId, string circuitId,
            out TrackInterlockingCircuitPassageState passage)
        {
            passage = default;
            return !string.IsNullOrEmpty(circuitId) && TryGetRouteStatus(context, routeId, out var status) &&
                status.IsRouteSet && status.CircuitPassageById.TryGetValue(circuitId, out passage);
        }

        public static bool CanRequestTurnoutPosition(TrackInterlockingContext context, string routeId, string connectionId)
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
                    return TrackInterlockingSignalLogic.CanRequestTurnoutPosition(context, routeId,
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
                        return TrackInterlockingSignalLogic.CanRequestTurnoutPosition(context, routeId,
                            connectionId, turnout.requiredPosition);
                    }
                }
            }
            return false;
        }

        private static void UpdateAfterOperation(TrackInterlockingContext context)
        {
            // 外部操作は公開値を撤回するだけで、通過・解錠・時素を進めない。
            TrackInterlockingValidationLogic.Update(context, 0f);
            TrackInterlockingSignalLogic.Update(context, false);
            Publish(context);
        }

        private static void Publish(TrackInterlockingContext context)
        {
            bool healthy = context.State.validation.isDefinitionValid && context.State.validation.hasValidTime;
            foreach (bool available in context.State.validation.availableByRouteId.Values)
            {
                healthy &= available;
            }
            context.State.isInterlockingHealthy = healthy;
            context.State.stateRevision++;
            TrackInterlockingOutputLogic.UpdateOutput(context);
        }

        private static void RemoveReleasedRoutes(TrackInterlockingContext context)
        {
            var finished = new List<string>();
            foreach (var pair in context.State.routeLock.routes)
            {
                if (!pair.Value.isLocked && !context.State.approachLock.routes[pair.Key].isLocked &&
                    !context.State.overrunProtection.routes[pair.Key].IsHeld &&
                    !TrackInterlockingReservationLogic.HasReservations(context, pair.Key))
                {
                    finished.Add(pair.Key);
                }
            }
            foreach (string routeId in finished)
            {
                TrackInterlockingPassageLogic.Remove(context, routeId);
                TrackInterlockingApproachLockLogic.Remove(context, routeId);
                TrackInterlockingOverrunProtectionLogic.Remove(context, routeId);
                TrackInterlockingSignalLogic.ResetReservation(context, routeId);
                TrackInterlockingRouteLockLogic.Remove(context, routeId);
            }
        }
    }
}
