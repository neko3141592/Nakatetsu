using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    public static class TrackInterlockingLogic
    {
        public static bool TryInitialize(TrackInterlockingContext context,
            TrackInterlockingDefinition definition, out string error)
        {
            context.RoutesById.Clear();
            context.RouteStatesById.Clear();
            context.TurnoutTrackCircuitLocksByConnectionId.Clear();
            context.IsInitialized = false;

            if (definition?.routes == null || definition.turnoutTrackCircuitLocks == null)
            {
                error = "Interlocking definition is missing.";
                return false;
            }

            foreach (InterlockingRoute route in definition.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.routeId) ||
                    !context.RoutesById.TryAdd(route.routeId, route))
                {
                    context.RoutesById.Clear();
                    error = "Route IDs must be nonempty and unique.";
                    return false;
                }
            }

            foreach (TurnoutTrackCircuitLock turnoutLock in definition.turnoutTrackCircuitLocks)
            {
                if (turnoutLock == null || string.IsNullOrWhiteSpace(turnoutLock.connectionId) ||
                    turnoutLock.trackCircuitIds == null || turnoutLock.trackCircuitIds.Count == 0 ||
                    !context.TurnoutTrackCircuitLocksByConnectionId.TryAdd(turnoutLock.connectionId, turnoutLock))
                {
                    error = "Turnout track circuit locks must have unique connection IDs and at least one circuit.";
                    return false;
                }

                foreach (string circuitId in turnoutLock.trackCircuitIds)
                {
                    if (!string.IsNullOrWhiteSpace(circuitId)) continue;
                    error = $"Turnout '{turnoutLock.connectionId}' has an empty track circuit ID.";
                    return false;
                }
            }

            foreach (InterlockingRoute route in definition.routes)
            {
                if (!TryValidateTurnoutLocks(context, route.requiredTurnouts, out error))
                {
                    return false;
                }

                if (!TryValidateOverrunDefinition(context, route, out error))
                {
                    return false;
                }
            }

            context.IsInitialized = true;
            error = null;
            return true;
        }

        public static bool CanRequestTurnoutPosition(TrackInterlockingContext context,
            TrackCircuitSimulationState circuits, string routeId, string connectionId)
        {
            if (context == null || !context.IsInitialized || circuits == null ||
                string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(connectionId) ||
                !context.RoutesById.TryGetValue(routeId, out InterlockingRoute route) ||
                !context.RouteStatesById.TryGetValue(routeId, out TrackInterlockingRouteState state) ||
                !CanSetRouteTurnouts(route, circuits, state) ||
                route.requiredTurnouts == null ||
                !context.TurnoutTrackCircuitLocksByConnectionId.TryGetValue(connectionId, out var turnoutLock))
                return false;

            // 本進路と、選択した防護方式で保持する転轍機だけを転換する。
            if (!AreOverrunCircuitsClear(route, circuits, state) || state.OverrunAutomaticReleaseBlocked)
            {
                return false;
            }

            bool requiredByRoute = false;
            foreach (TurnoutRequirement turnout in GetRequiredTurnouts(route, state))
            {
                requiredByRoute |= turnout != null && turnout.connectionId == connectionId;
            }
            if (!requiredByRoute) return false;

            if (turnoutLock.trackCircuitIds == null || turnoutLock.trackCircuitIds.Count == 0)
                return false;

            foreach (string circuitId in turnoutLock.trackCircuitIds)
                if (circuits.IsOccupied(circuitId)) return false;

            return true;
        }


        private static bool TryValidateTurnoutLocks(
            TrackInterlockingContext context,
            List<TurnoutRequirement> requiredTurnouts,
            out string error)
        {
            if (requiredTurnouts == null)
            {
                error = "Turnout requirements are missing.";
                return false;
            }

            foreach (TurnoutRequirement turnout in requiredTurnouts)
            {
                if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId) ||
                    !context.TurnoutTrackCircuitLocksByConnectionId.ContainsKey(turnout.connectionId))
                {
                    error = "A required turnout has no track circuit lock.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateOverrunDefinition(
            TrackInterlockingContext context,
            InterlockingRoute route,
            out string error)
        {
            error = null;
            var protection = route.overrunProtection;
            if (protection == null)
            {
                return true;
            }

            // 過走防護用の転轍機にも、必ずてっ査鎖錠を定義する。
            if (protection.common?.clearTrackCircuitIds == null ||
                protection.normalAdditional?.clearTrackCircuitIds == null || protection.release == null)
            {
                error = $"Route '{route.routeId}' has an incomplete overrun protection definition.";
                return false;
            }

            if (!TryValidateTurnoutLocks(context, protection.common.requiredTurnouts, out error) ||
                !TryValidateTurnoutLocks(context, protection.normalAdditional.requiredTurnouts, out error))
            {
                return false;
            }

            var release = protection.release;
            if ((release.mode != OverrunReleaseMode.WithRouteRelease &&
                 release.mode != OverrunReleaseMode.TimedAfterArrival) ||
                release.releaseSeconds < 0f || float.IsNaN(release.releaseSeconds) ||
                float.IsInfinity(release.releaseSeconds))
            {
                error = $"Route '{route.routeId}' has an invalid overrun release definition.";
                return false;
            }

            // 到着は本進路の通過状態で検出する。追跡できない回路では時素を開始しない。
            if (release.mode == OverrunReleaseMode.TimedAfterArrival &&
                (string.IsNullOrWhiteSpace(release.triggerTrackCircuitId) ||
                 route.routeLockTrackCircuitIds == null ||
                 !route.routeLockTrackCircuitIds.Contains(release.triggerTrackCircuitId)))
            {
                error = $"Route '{route.routeId}' has an overrun arrival trigger outside its main route.";
                return false;
            }

            return true;
        }

        internal static IEnumerable<TurnoutRequirement> GetRequiredTurnouts(
            InterlockingRoute route,
            TrackInterlockingRouteState state)
        {
            if ((state.RouteLocked || state.ApproachLocked) && route.requiredTurnouts != null)
            {
                foreach (TurnoutRequirement turnout in route.requiredTurnouts)
                {
                    yield return turnout;
                }
            }

            if (!IsOverrunProtectionHeld(state))
            {
                yield break;
            }

            if (route.overrunProtection?.common?.requiredTurnouts != null)
            {
                foreach (TurnoutRequirement turnout in route.overrunProtection.common.requiredTurnouts)
                {
                    yield return turnout;
                }
            }

            if (state.overrunProtectionMode == OverrunProtectionMode.Normal &&
                route.overrunProtection?.normalAdditional?.requiredTurnouts != null)
            {
                foreach (TurnoutRequirement turnout in route.overrunProtection.normalAdditional.requiredTurnouts)
                {
                    yield return turnout;
                }
            }
        }

        public static bool TryRequestRoute(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            string routeId,
            out string error)
        {

            // 初期化状態・重複要求・入力と定義を確認し、要求された進路を取得する。
            if (!TryValidateRouteRequest(context, circuits, connections, routeId, out var route, out error))
            {
                return false;
            }

            // 本進路の軌道回路が有効で空いていることを確認し、予約対象を集める。
            if (!TryCollectRouteCircuits(route, circuits, out var routeCircuitIds, out error))
            {
                return false;
            }

            // 本進路の転轍機の存在と要求位置の矛盾を確認し、要求位置を集める。
            if (!TryCollectRequiredTurnoutPositions(route, connections, out var requiredPositions, out error))
            {
                return false;
            }

            // 他進路の本進路・保持中の過走防護との競合を確認する。
            if (!TryCheckRouteConflicts(context, route, routeCircuitIds, requiredPositions, out error))
            {
                return false;
            }

            // 共通設備、通常用追加設備の順に照査して、確保可能な防護方式を選ぶ。
            if (!TrySelectOverrunProtectionMode(context, circuits, connections, route, requiredPositions,
                    out var overrunProtectionMode, out error))
            {
                return false;
            }

            // 全照査が通った進路を、未進入の状態で予約する。
            ReserveRoute(context, routeId, routeCircuitIds, overrunProtectionMode);
            error = null;
            return true;
        }

        private static bool TryValidateRouteRequest(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            string routeId,
            out InterlockingRoute route,
            out string error)
        {
            route = null;
            // 初期化状態・進路IDの照査
            if (context == null || !context.IsInitialized ||
                string.IsNullOrWhiteSpace(routeId) ||
                !context.RoutesById.TryGetValue(routeId, out route))
            {
                error = $"Route '{routeId}' is unavailable or interlocking is not initialized.";
                return false;
            }

            // 二重予約の照査
            if (context.RouteStatesById.ContainsKey(routeId))
            {
                error = $"Route '{routeId}' is already requested.";
                return false;
            }

            // 線路状態入力の照査
            if (circuits == null || connections == null || !connections.IsInitialized)
            {
                error = "Track circuits or connections are unavailable.";
                return false;
            }

            // 本進路定義の欠損照査
            if (route.routeLockTrackCircuitIds == null || route.routeLockTrackCircuitIds.Count == 0 ||
                route.requiredTurnouts == null)
            {
                error = $"Route '{routeId}' has an incomplete definition.";
                return false;
            }

            if (!TryValidateOverrunDefinition(context, route, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryCollectRouteCircuits(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            out HashSet<string> routeCircuitIds,
            out string error)
        {
            string routeId = route.routeId;
            routeCircuitIds = new HashSet<string>();
            foreach (string circuitId in route.routeLockTrackCircuitIds)
            {
                // 本進路の回路ID・重複照査
                if (string.IsNullOrWhiteSpace(circuitId) || !routeCircuitIds.Add(circuitId))
                {
                    error = $"Route '{routeId}' has an invalid or duplicate track circuit ID.";
                    return false;
                }

                // 対象回路の空き照査（状態不明も拒否）
                if (circuits.IsOccupied(circuitId))
                {
                    error = $"Track circuit '{circuitId}' is occupied or unavailable.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryCollectRequiredTurnoutPositions(
            InterlockingRoute route,
            TrackConnectionContext connections,
            out Dictionary<string, TrackSwitchPosition> requiredPositions,
            out string error)
        {
            string routeId = route.routeId;
            requiredPositions = new Dictionary<string, TrackSwitchPosition>();
            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                // 本進路の転轍機要求・位置矛盾の照査
                if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId) ||
                    (turnout.requiredPosition != TrackSwitchPosition.Normal &&
                     turnout.requiredPosition != TrackSwitchPosition.Reverse) ||
                    (requiredPositions.TryGetValue(turnout.connectionId, out var position) &&
                     position != turnout.requiredPosition))
                {
                    error = $"Route '{routeId}' has an invalid turnout requirement.";
                    return false;
                }

                requiredPositions[turnout.connectionId] = turnout.requiredPosition;
                // 本進路の転轍機状態の取得可否照査
                if (!connections.TryGetState(turnout.connectionId, out _))
                {
                    error = $"Switch '{turnout.connectionId}' is unavailable.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryCheckRouteConflicts(
            TrackInterlockingContext context,
            InterlockingRoute requestedRoute,
            HashSet<string> requestedCircuitIds,
            Dictionary<string, TrackSwitchPosition> requestedTurnoutPositions,
            out string error)
        {
            string requestedRouteId = requestedRoute.routeId;
            // TODO: 後続進路との共同保持条件を実装する。それまでは回路の重複を拒否する。
            foreach (var (existingRouteId, existingRouteState) in context.RouteStatesById)
            {
                // 既存進路定義の欠損照査
                if (!context.RoutesById.TryGetValue(existingRouteId, out InterlockingRoute existingRoute) ||
                    existingRoute.routeLockTrackCircuitIds == null || existingRoute.requiredTurnouts == null)
                {
                    error = $"Existing route '{existingRouteId}' has an incomplete definition.";
                    return false;
                }

                if (existingRouteState.RouteLocked || existingRouteState.ApproachLocked)
                {
                    // 本進路同士の絶対競合照査
                    if ((requestedRoute.conflictRouteIds != null && requestedRoute.conflictRouteIds.Contains(existingRouteId)) ||
                        (existingRoute.conflictRouteIds != null && existingRoute.conflictRouteIds.Contains(requestedRouteId)))
                    {
                        error = $"Route '{requestedRouteId}' conflicts with existing route '{existingRouteId}'.";
                        return false;
                    }

                    foreach (string circuitId in existingRoute.routeLockTrackCircuitIds)
                    {
                        // 既存本進路との回路予約重複照査
                        if (requestedCircuitIds.Contains(circuitId))
                        {
                            error = $"Track circuit '{circuitId}' is reserved by route '{existingRouteId}'.";
                            return false;
                        }
                    }

                    foreach (TurnoutRequirement turnout in existingRoute.requiredTurnouts)
                    {
                        // 既存本進路の転轍機要求の欠損照査
                        if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId))
                        {
                            error = $"Existing route '{existingRouteId}' has an invalid turnout requirement.";
                            return false;
                        }

                        // 既存本進路との転轍機位置競合照査
                        if (requestedTurnoutPositions.TryGetValue(turnout.connectionId, out var position) &&
                            position != turnout.requiredPosition)
                        {
                            error = $"Switch '{turnout.connectionId}' is locked in another position by route '{existingRouteId}'.";
                            return false;
                        }
                    }
                }

                // 保持中の過走防護が要求する転轍機位置との競合を確認する。
                if (!TryCheckOverrunTurnoutConflicts(existingRouteId, existingRoute, existingRouteState, requestedTurnoutPositions, out error))
                {
                    return false;
                }

                // 新しく要求する進路と既存の過走防護進路の重複を調査する
                if (!TryCheckOverrunCircuitConflicts(existingRouteId, existingRoute, existingRouteState,
                        requestedCircuitIds, out error))
                {
                    return false;
                }

            }

            error = null;
            return true;
        }

        private static bool TryCheckOverrunCircuitConflicts(
            string existingRouteId,
            InterlockingRoute existingRoute,
            TrackInterlockingRouteState existingRouteState,
            HashSet<string> requestedCircuitIds,
            out string error)
        {
            if (existingRouteState.overrunProtectionPhase == OverrunProtectionPhase.None ||
                existingRouteState.overrunProtectionPhase == OverrunProtectionPhase.Released)
            {
                error = null;
                return true;
            }

            var existingOverrunProtection = existingRoute.overrunProtection;
            if (existingOverrunProtection?.common?.clearTrackCircuitIds == null ||
                (existingRouteState.overrunProtectionMode == OverrunProtectionMode.Normal &&
                 existingOverrunProtection.normalAdditional?.clearTrackCircuitIds == null))
            {
                error = $"Existing route '{existingRouteId}' has an incomplete overrun protection definition.";
                return false;
            }

            foreach (string requestedCircuitId in requestedCircuitIds)
            {
                if (existingOverrunProtection.common.clearTrackCircuitIds.Contains(requestedCircuitId) ||
                    (existingRouteState.overrunProtectionMode == OverrunProtectionMode.Normal &&
                     existingOverrunProtection.normalAdditional.clearTrackCircuitIds.Contains(requestedCircuitId)))
                {
                    error = $"Track circuit '{requestedCircuitId}' is reserved by overrun protection of route '{existingRouteId}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryCheckOverrunTurnoutConflicts(
            string existingRouteId,
            InterlockingRoute existingRoute,
            TrackInterlockingRouteState existingRouteState,
            Dictionary<string, TrackSwitchPosition> requestedTurnoutPositions,
            out string error)
        {
            // 過走防護がない場合や、過走防護が既に解除されてる場合は無視
            if (existingRouteState.overrunProtectionPhase == OverrunProtectionPhase.None ||
                existingRouteState.overrunProtectionPhase == OverrunProtectionPhase.Released)
            {
                error = null;
                return true;
            }

            // 共通防護の転轍機リストの欠損照査
            if (existingRoute.overrunProtection?.common?.requiredTurnouts == null)
            {
                error = $"Existing route '{existingRouteId}' has an incomplete overrun protection definition.";
                return false;
            }

            foreach (var turnoutRequirement in existingRoute.overrunProtection.common.requiredTurnouts)
            {
                // 過走防護の転轍機ID・要求位置の照査
                if (turnoutRequirement == null || string.IsNullOrWhiteSpace(turnoutRequirement.connectionId) ||
                    (turnoutRequirement.requiredPosition != TrackSwitchPosition.Normal &&
                     turnoutRequirement.requiredPosition != TrackSwitchPosition.Reverse))
                {
                    error = $"Existing route '{existingRouteId}' has an invalid overrun turnout requirement.";
                    return false;
                }


                // 既存過走防護との転轍機位置競合照査
                if (requestedTurnoutPositions.TryGetValue(turnoutRequirement.connectionId, out var position) &&
                    position != turnoutRequirement.requiredPosition)
                {
                    error = $"Switch '{turnoutRequirement.connectionId}' is locked in another position by overrun protection of route '{existingRouteId}'.";
                    return false;
                }
            }

            // Normalで保持している通常用追加設備も確認する。
            if (existingRouteState.overrunProtectionMode == OverrunProtectionMode.Normal)
            {
                // 通常用追加防護の転轍機リストの欠損照査
                if (existingRoute.overrunProtection.normalAdditional?.requiredTurnouts == null)
                {
                    error = $"Existing route '{existingRouteId}' has an incomplete normal overrun protection definition.";
                    return false;
                }

                foreach (var turnoutRequirement in existingRoute.overrunProtection.normalAdditional.requiredTurnouts)
                {
                    // 過走防護の転轍機ID・要求位置の照査
                    if (turnoutRequirement == null || string.IsNullOrWhiteSpace(turnoutRequirement.connectionId) ||
                        (turnoutRequirement.requiredPosition != TrackSwitchPosition.Normal &&
                         turnoutRequirement.requiredPosition != TrackSwitchPosition.Reverse))
                    {
                        error = $"Existing route '{existingRouteId}' has an invalid overrun turnout requirement.";
                        return false;
                    }

                    // 既存過走防護との転轍機位置競合照査
                    if (requestedTurnoutPositions.TryGetValue(turnoutRequirement.connectionId, out var position) &&
                        position != turnoutRequirement.requiredPosition)
                    {
                        error = $"Switch '{turnoutRequirement.connectionId}' is locked in another position by overrun protection of route '{existingRouteId}'.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TrySelectOverrunProtectionMode(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            InterlockingRoute route,
            Dictionary<string, TrackSwitchPosition> requiredPositions,
            out OverrunProtectionMode overrunProtectionMode,
            out string error)
        {
            overrunProtectionMode = OverrunProtectionMode.None;
            var overrunProtection = route.overrunProtection;
            // 過走防護定義の有無を確認
            if (overrunProtection == null)
            {
                error = null;
                return true;
            }

            var commonCircuitIds = new HashSet<string>();
            var normalCircuitIds = new HashSet<string>();
            var commonPositions = new Dictionary<string, TrackSwitchPosition>(requiredPositions);

            // 共通設備の定義を確認し、本進路と矛盾しない要求位置を集める。
            if (!TryCollectOverrunResources(overrunProtection.common, connections,
                    commonCircuitIds, commonPositions, out error))
            {
                return false;
            }

            var normalPositions = new Dictionary<string, TrackSwitchPosition>(commonPositions);
            // 通常用追加設備の定義も先に確認し、定義不備をRestrictedへの切替理由にしない。
            if (!TryCollectOverrunResources(overrunProtection.normalAdditional, connections,
                    normalCircuitIds, normalPositions, out error))
            {
                return false;
            }

            // 共通設備が確保できなければ、どちらの方式も選べない。
            if (!TryCheckOverrunResources(context, circuits, route, commonCircuitIds, commonPositions, out error))
            {
                return false;
            }

            // Restrictedの追加制御と要求側の許可を確認できるまでは、Normal不可なら予約しない。
            if (!TryCheckOverrunResources(context, circuits, route, normalCircuitIds, normalPositions, out error))
            {
                error += " Restricted overrun protection is unavailable because its additional control and request authorization are not implemented.";
                return false;
            }

            overrunProtectionMode = OverrunProtectionMode.Normal;
            error = null;
            return true;
        }


        private static bool TryCollectOverrunResources(
            OverrunProtectionResources resources,
            TrackConnectionContext connections,
            HashSet<string> circuitIds,
            Dictionary<string, TrackSwitchPosition> requiredPositions,
            out string error)
        {
            // 過走防護の設備リストの欠損照査
            if (resources?.clearTrackCircuitIds == null || resources.requiredTurnouts == null)
            {
                error = "Overrun protection resources are missing.";
                return false;
            }

            foreach (string circuitId in resources.clearTrackCircuitIds)
            {
                // 過走防護の回路ID・重複照査
                if (string.IsNullOrWhiteSpace(circuitId) || !circuitIds.Add(circuitId))
                {
                    error = "Overrun protection has an invalid or duplicate track circuit ID.";
                    return false;
                }
            }

            foreach (var requiredTurnout in resources.requiredTurnouts)
            {
                // 過走防護の転轍機要求・位置矛盾の照査
                if (requiredTurnout == null || string.IsNullOrWhiteSpace(requiredTurnout.connectionId) ||
                    (requiredTurnout.requiredPosition != TrackSwitchPosition.Normal &&
                     requiredTurnout.requiredPosition != TrackSwitchPosition.Reverse) ||
                    (requiredPositions.TryGetValue(requiredTurnout.connectionId, out var position) &&
                     position != requiredTurnout.requiredPosition))
                {
                    error = "Overrun protection has an invalid or conflicting turnout requirement.";
                    return false;
                }

                // 過走防護の転轍機状態の取得可否照査
                if (!connections.TryGetState(requiredTurnout.connectionId, out _))
                {
                    error = $"Switch '{requiredTurnout.connectionId}' is unavailable.";
                    return false;
                }

                requiredPositions[requiredTurnout.connectionId] = requiredTurnout.requiredPosition;
            }

            error = null;
            return true;
        }

        private static bool TryCheckOverrunResources(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            InterlockingRoute route,
            HashSet<string> circuitIds,
            Dictionary<string, TrackSwitchPosition> requiredPositions,
            out string error)
        {
            foreach (string circuitId in circuitIds)
            {
                // 対象回路の空き照査（状態不明も拒否）
                if (circuits.IsOccupied(circuitId))
                {
                    error = $"Overrun track circuit '{circuitId}' is occupied or unavailable.";
                    return false;
                }
            }

            // 本進路と同じ判定で、既存の本進路・保持中の過走防護との競合を照査する。
            return TryCheckRouteConflicts(context, route, circuitIds, requiredPositions, out error);
        }

        private static void ReserveRoute(
            TrackInterlockingContext context,
            string routeId,
            HashSet<string> routeCircuitIds,
            OverrunProtectionMode overrunProtectionMode)
        {
            var state = new TrackInterlockingRouteState
            {
                RouteLocked = true,
                overrunProtectionMode = overrunProtectionMode
            };
            foreach (string circuitId in routeCircuitIds)
            {
                state.CircuitPassageById.Add(circuitId, TrackInterlockingCircuitPassageState.NotEntered);
            }

            if (overrunProtectionMode == OverrunProtectionMode.Normal || overrunProtectionMode == OverrunProtectionMode.Restricted)
            {
                state.overrunProtectionPhase = OverrunProtectionPhase.Setting;
            }
            else
            {
                state.overrunProtectionPhase = OverrunProtectionPhase.None;
            }


            context.RouteStatesById.Add(routeId, state);
        }

        public static bool TryCancelRoute(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            string routeId,
            out string error)
        {
            if (context == null || !context.IsInitialized ||
                string.IsNullOrWhiteSpace(routeId) ||
                !context.RouteStatesById.TryGetValue(routeId, out var state) ||
                !context.RoutesById.TryGetValue(routeId, out var route))
            {
                error = $"Route '{routeId}' has no existing reservation.";
                return false;
            }

            if (state.CancelPending)
            {
                error = null;
                return true;
            }

            if (circuits == null)
            {
                error = "Track circuits are unavailable.";
                return false;
            }

            state.CancelPending = true;
            state.ProceedAllowed = false;

            if (route.approachLock?.trackCircuitIds != null)
            {
                foreach (string circuitId in route.approachLock.trackCircuitIds)
                {
                    if (!circuits.IsOccupied(circuitId)) continue;

                    state.ApproachLocked = true;
                    state.ApproachReleaseRemainingSeconds = Mathf.Max(0f, route.approachLock.releaseSeconds);
                    break;
                }
            }

            error = null;
            return true;
        }



        // ここからは毎tick行う処理群
        public static void Calculate(
            TrackInterlockingContext context,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            float deltaTimeSeconds)
        {
            var routeIdsToDelete = new List<string>();
            foreach (var pair in context.RouteStatesById)
            {
                string routeId = pair.Key;
                TrackInterlockingRouteState state = pair.Value;

                InterlockingRoute route = context.RoutesById[routeId];

                UpdateTrackCircuitState(
                    route,
                    circuits,
                    state
                );

                UpdateApproachLockState(
                    state,
                    deltaTimeSeconds
                );

                UpdateRouteLockState(
                    route,
                    circuits,
                    connections,
                    state
                );

                UpdateOverrunProtectionState(
                    route,
                    circuits,
                    connections,
                    state,
                    deltaTimeSeconds
                );

                UpdatePathEstablished(
                    route,
                    circuits,
                    connections,
                    state
                );

                UpdateProceedAllow(
                    route,
                    circuits,
                    connections,
                    state
                );

                if (CanDeleteRouteState(state))
                    routeIdsToDelete.Add(routeId);

            }

            foreach (string routeId in routeIdsToDelete)
                context.RouteStatesById.Remove(routeId);
        }

        public static void UpdateTrackCircuitState(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state
        )
        {
            // 本進路の解錠後は、別列車の在線で旧進路を再鎖錠しない。
            if (!state.RouteLocked || route.routeLockTrackCircuitIds == null || circuits == null)
            {
                return;
            }

            foreach (string trackCircuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(trackCircuitId) ||
                    !state.CircuitPassageById.TryGetValue(trackCircuitId, out var passageState) ||
                    !circuits.OccupiedByCircuitId.TryGetValue(trackCircuitId, out bool occupied))
                {
                    continue;
                }

                // 成立後、この進路の到着回路に初めて進入した時だけ時素の開始を記録する。
                if (occupied && passageState == TrackInterlockingCircuitPassageState.NotEntered &&
                    state.overrunProtectionPhase == OverrunProtectionPhase.Established &&
                    route.overrunProtection?.release?.mode == OverrunReleaseMode.TimedAfterArrival &&
                    route.overrunProtection.release.triggerTrackCircuitId == trackCircuitId)
                {
                    state.OverrunArrivalDetected = true;
                }

                if (occupied)
                {
                    state.CircuitPassageById[trackCircuitId] = TrackInterlockingCircuitPassageState.Occupied;
                    state.RouteLocked = true;
                }
                else if (passageState == TrackInterlockingCircuitPassageState.Occupied)
                {
                    state.CircuitPassageById[trackCircuitId] = TrackInterlockingCircuitPassageState.Passed;
                }
            }
        }

        public static void UpdateApproachLockState(
            TrackInterlockingRouteState state,
            float deltaTimeSeconds
        )
        {
            if (!state.ApproachLocked || !IsValidDeltaTime(deltaTimeSeconds))
            {
                return;
            }

            state.ApproachReleaseRemainingSeconds = Mathf.Max(
                0f,
                state.ApproachReleaseRemainingSeconds - deltaTimeSeconds
            );

            if (state.ApproachReleaseRemainingSeconds == 0f)
            {
                state.ApproachLocked = false;
            }

        }

        public static void UpdateRouteLockState(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state
        )
        {
            if (!state.RouteLocked || circuits == null || route.routeLockTrackCircuitIds == null ||
                route.routeLockTrackCircuitIds.Count == 0)
            {
                return;
            }

            bool allPassed = true;
            bool allNotEntered = true;
            foreach (string trackCircuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(trackCircuitId) ||
                    !state.CircuitPassageById.TryGetValue(trackCircuitId, out var passageState) ||
                    circuits.IsOccupied(trackCircuitId))
                {
                    return;
                }

                allPassed &= passageState == TrackInterlockingCircuitPassageState.Passed;
                allNotEntered &= passageState == TrackInterlockingCircuitPassageState.NotEntered;
            }


            // 取消時は、すでに始まった本進路の転換動作も完了するまで保持する。
            if (!state.ApproachLocked && (allPassed ||
                (state.CancelPending && allNotEntered && AreTurnoutsConfirmed(route.requiredTurnouts, connections, false))))
            {
                state.RouteLocked = false;
            }
        }

        public static void UpdateOverrunProtectionState(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state,
            float deltaTimeSeconds)
        {
            if (!IsOverrunProtectionHeld(state) || route.overrunProtection?.release == null)
            {
                return;
            }

            bool circuitsClear = AreOverrunCircuitsClear(route, circuits, state);

            // 成立後の過走・状態不明は記録を残し、回路が空いても自動解錠を再開しない。
            if (state.overrunProtectionPhase != OverrunProtectionPhase.Setting && !circuitsClear)
            {
                state.OverrunAutomaticReleaseBlocked = true;
            }

            if (state.OverrunAutomaticReleaseBlocked)
            {
                return;
            }

            // 未進入取消では、接近鎖錠と転換動作の終了を待つ。
            // 未成立の転轍機は、要求位置まで転換し直さず、確定位置に停止していればよい。
            if (state.CancelPending && !HasRouteEntered(state))
            {
                if (!state.RouteLocked && !state.ApproachLocked && circuitsClear &&
                    AreOverrunTurnoutsConfirmed(route, connections, state, false))
                {
                    state.overrunProtectionPhase = OverrunProtectionPhase.Released;
                    state.OverrunProtectionReleaseRemainingSeconds = 0f;
                }
                return;
            }

            bool turnoutsConfirmed = AreOverrunTurnoutsConfirmed(route, connections, state, true);
            if (state.overrunProtectionPhase == OverrunProtectionPhase.Setting)
            {
                if (circuitsClear && turnoutsConfirmed && CanSetRouteTurnouts(route, circuits, state))
                {
                    state.overrunProtectionPhase = OverrunProtectionPhase.Established;
                }
                return;
            }

            var release = route.overrunProtection.release;
            if (release.mode == OverrunReleaseMode.WithRouteRelease)
            {
                if (!state.RouteLocked && !state.ApproachLocked && circuitsClear && turnoutsConfirmed)
                {
                    state.overrunProtectionPhase = OverrunProtectionPhase.Released;
                }
                return;
            }

            if (release.mode != OverrunReleaseMode.TimedAfterArrival)
            {
                return;
            }

            if (state.overrunProtectionPhase == OverrunProtectionPhase.Established && state.OverrunArrivalDetected)
            {
                state.overrunProtectionPhase = OverrunProtectionPhase.ReleaseTiming;
                state.OverrunProtectionReleaseRemainingSeconds = release.releaseSeconds;
                // 到着を検出したtickでは、それ以前の経過時間を時素から引かない。
                return;
            }

            if (state.overrunProtectionPhase != OverrunProtectionPhase.ReleaseTiming)
            {
                return;
            }

            if (IsValidDeltaTime(deltaTimeSeconds))
            {
                state.OverrunProtectionReleaseRemainingSeconds = Mathf.Max(
                    0f,
                    state.OverrunProtectionReleaseRemainingSeconds - deltaTimeSeconds
                );
            }

            // 時素が満了しても、設備の照査が通るまでは防護を保持する。
            if (state.OverrunProtectionReleaseRemainingSeconds == 0f &&
                !state.ApproachLocked && circuitsClear && turnoutsConfirmed)
            {
                state.overrunProtectionPhase = OverrunProtectionPhase.Released;
            }
        }

        public static void UpdatePathEstablished(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state)
        {
            state.PathEstablished = false;
            if (!state.RouteLocked || circuits == null || route.routeLockTrackCircuitIds == null ||
                route.routeLockTrackCircuitIds.Count == 0 ||
                !AreTurnoutsConfirmed(route.requiredTurnouts, connections, true))
            {
                return;
            }

            // 本進路への進入後も開通状態を保持する。ただし、回路の状態不明は許可しない。
            foreach (string circuitId in route.routeLockTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(circuitId) || !circuits.OccupiedByCircuitId.ContainsKey(circuitId))
                {
                    return;
                }
            }

            if (state.overrunProtectionPhase == OverrunProtectionPhase.None)
            {
                if (state.overrunProtectionMode != OverrunProtectionMode.None)
                {
                    return;
                }
            }
            else if (state.overrunProtectionPhase != OverrunProtectionPhase.Released)
            {
                if ((state.overrunProtectionPhase != OverrunProtectionPhase.Established &&
                     state.overrunProtectionPhase != OverrunProtectionPhase.ReleaseTiming) ||
                    state.OverrunAutomaticReleaseBlocked || !AreOverrunCircuitsClear(route, circuits, state) ||
                    !AreOverrunTurnoutsConfirmed(route, connections, state, true))
                {
                    return;
                }
            }

            state.PathEstablished = true;
        }

        private static bool AreOverrunCircuitsClear(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state)
        {
            if (!IsOverrunProtectionHeld(state))
            {
                return true;
            }

            var protection = route.overrunProtection;
            if (!AreProtectionCircuitsClear(protection?.common, circuits))
            {
                return false;
            }

            return state.overrunProtectionMode == OverrunProtectionMode.Restricted ||
                state.overrunProtectionMode == OverrunProtectionMode.Normal &&
                AreProtectionCircuitsClear(protection.normalAdditional, circuits);
        }

        private static bool AreProtectionCircuitsClear(
            OverrunProtectionResources resources,
            TrackCircuitSimulationState circuits)
        {
            if (resources?.clearTrackCircuitIds == null || circuits == null)
            {
                return false;
            }

            foreach (string circuitId in resources.clearTrackCircuitIds)
            {
                if (string.IsNullOrWhiteSpace(circuitId) || circuits.IsOccupied(circuitId))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreOverrunTurnoutsConfirmed(
            InterlockingRoute route,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state,
            bool requirePosition)
        {
            var protection = route.overrunProtection;
            if (!AreTurnoutsConfirmed(protection?.common?.requiredTurnouts, connections, requirePosition))
            {
                return false;
            }

            return state.overrunProtectionMode == OverrunProtectionMode.Restricted ||
                state.overrunProtectionMode == OverrunProtectionMode.Normal &&
                AreTurnoutsConfirmed(protection.normalAdditional?.requiredTurnouts, connections, requirePosition);
        }

        private static bool AreTurnoutsConfirmed(
            List<TurnoutRequirement> requiredTurnouts,
            TrackConnectionContext connections,
            bool requirePosition)
        {
            if (requiredTurnouts == null || connections == null || !connections.IsInitialized)
            {
                return false;
            }

            foreach (TurnoutRequirement turnout in requiredTurnouts)
            {
                if (turnout == null || string.IsNullOrWhiteSpace(turnout.connectionId) ||
                    (turnout.requiredPosition != TrackSwitchPosition.Normal &&
                     turnout.requiredPosition != TrackSwitchPosition.Reverse) ||
                    !connections.TryGetState(turnout.connectionId, out var switchState) || switchState.IsMoving ||
                    (switchState.ActualPosition != TrackSwitchPosition.Normal &&
                     switchState.ActualPosition != TrackSwitchPosition.Reverse) ||
                    (requirePosition && switchState.ActualPosition != turnout.requiredPosition))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasRouteEntered(TrackInterlockingRouteState state)
        {
            foreach (var passage in state.CircuitPassageById.Values)
            {
                if (passage != TrackInterlockingCircuitPassageState.NotEntered)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOverrunProtectionHeld(TrackInterlockingRouteState state) =>
            state.overrunProtectionPhase != OverrunProtectionPhase.None &&
            state.overrunProtectionPhase != OverrunProtectionPhase.Released;

        private static bool IsValidDeltaTime(float deltaTimeSeconds) =>
            deltaTimeSeconds >= 0f && !float.IsNaN(deltaTimeSeconds) && !float.IsInfinity(deltaTimeSeconds);

        public static void UpdateProceedAllow(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackConnectionContext connections,
            TrackInterlockingRouteState state
        )
        {
            state.ProceedAllowed = false;
            // TODO: Restrictedの追加ATC制御が適用済みか照査する。未接続の間は進行許可を出さない。
            if (state.overrunProtectionMode == OverrunProtectionMode.Restricted)
            {
                return;
            }

            if (!state.PathEstablished || !CanSetRouteTurnouts(route, circuits, state) ||
                connections == null || route.requiredTurnouts == null)
                return;

            foreach (TurnoutRequirement turnout in route.requiredTurnouts)
            {
                if (turnout == null || turnout.requiredPosition == TrackSwitchPosition.Unknown ||
                    !connections.TryGetState(turnout.connectionId, out TrackConnectionState switchState) ||
                    switchState.IsMoving || switchState.ActualPosition != turnout.requiredPosition)
                    return;
            }

            state.ProceedAllowed = true;
        }

        internal static bool CanSetRouteTurnouts(
            InterlockingRoute route,
            TrackCircuitSimulationState circuits,
            TrackInterlockingRouteState state)
        {
            if (!state.RouteLocked || state.CancelPending || circuits == null ||
                route.routeLockTrackCircuitIds == null || route.routeLockTrackCircuitIds.Count == 0)
                return false;

            // 取消後や列車進入後は、進行許可がなくても転轍しない。
            foreach (string circuitId in route.routeLockTrackCircuitIds)
            {
                if (!state.CircuitPassageById.TryGetValue(circuitId, out var passage) ||
                    passage != TrackInterlockingCircuitPassageState.NotEntered ||
                    circuits.IsOccupied(circuitId))
                    return false;
            }

            return true;
        }

        private static bool CanDeleteRouteState(TrackInterlockingRouteState state) =>
            !state.ProceedAllowed && !state.ApproachLocked && !state.RouteLocked &&
            !IsOverrunProtectionHeld(state);
    }
}
