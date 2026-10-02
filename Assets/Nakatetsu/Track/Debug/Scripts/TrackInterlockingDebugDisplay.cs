using System.Collections.Generic;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Debug/Track Interlocking Debug Display")]
    public sealed class TrackInterlockingDebugDisplay : MonoBehaviour
    {
        [SerializeField] private TrackInterlockingController interlocking;
        [SerializeField] private TrackCircuitSimulationController circuits;
        [SerializeField] private TrackConnectionController connections;
        [SerializeField] private bool showInGameView = true;

        private Vector2 scrollPosition;
        private string lastResult;

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying || !showInGameView) return;

            var area = new Rect(Screen.width - 390f, 220f, 378f,
                Mathf.Clamp(Screen.height - 232f, 120f, 650f));
            GUILayout.BeginArea(area, "Interlocking", GUI.skin.window);

            if (interlocking == null || !interlocking.IsInitialized)
            {
                GUILayout.Label("Interlocking is not initialized.");
                GUILayout.EndArea();
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            foreach (InterlockingRoute route in interlocking.Routes)
            {
                if (route == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(route.routeId);

                if (interlocking.TryGetRouteState(route.routeId, out var state))
                {
                    GUILayout.Label($"Proceed: {state.ProceedAllowed}  Route lock: {state.RouteLocked}");
                    GUILayout.Label($"Path: {state.PathEstablished}  Overrun: {state.overrunProtectionMode} / {state.overrunProtectionPhase}");
                    GUILayout.Label($"Overrun release: {state.OverrunProtectionReleaseRemainingSeconds:F1}s  Blocked: {state.OverrunAutomaticReleaseBlocked}");
                    GUILayout.Label($"Cancel: {state.CancelPending}  Approach lock: {state.ApproachLocked}  {state.ApproachReleaseRemainingSeconds:F1}s");
                }
                else
                {
                    GUILayout.Label("Inactive");
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Request"))
                    lastResult = interlocking.TryRequestRoute(route.routeId, out string requestError)
                        ? $"{route.routeId}: requested" : requestError;
                if (GUILayout.Button("Cancel"))
                    lastResult = interlocking.TryCancelRoute(route.routeId, out string cancelError)
                        ? $"{route.routeId}: cancel accepted" : cancelError;
                GUILayout.EndHorizontal();

                if (route.routeLockTrackCircuitIds != null)
                {
                    var statuses = new List<string>();
                    foreach (string circuitId in route.routeLockTrackCircuitIds)
                    {
                        bool occupied = circuits == null || circuits.IsOccupied(circuitId);
                        string passage = interlocking.TryGetCircuitPassage(route.routeId, circuitId, out var value)
                            ? value.ToString() : "-";
                        statuses.Add($"{circuitId}:{(occupied ? "OCC" : "CLR")}/{passage}");
                    }
                    GUILayout.Label(string.Join("  ", statuses));
                }

                if (route.approachLock?.trackCircuitIds != null)
                {
                    foreach (string circuitId in route.approachLock.trackCircuitIds)
                    {
                        bool occupied = circuits == null || circuits.IsOccupied(circuitId);
                        GUILayout.Label($"Approach {circuitId}: {(occupied ? "OCC" : "CLR")}");
                    }
                }

                if (route.requiredTurnouts != null)
                {
                    foreach (TurnoutRequirement turnout in route.requiredTurnouts)
                    {
                        if (turnout == null) continue;
                        string actual = connections != null &&
                            connections.TryGetState(turnout.connectionId, out var switchState)
                            ? (switchState.IsMoving ? $"moving to {switchState.RequestedPosition}"
                                : switchState.ActualPosition.ToString())
                            : "unknown";
                        GUILayout.Label($"{turnout.connectionId}: {actual} / required {turnout.requiredPosition}");
                    }
                }

                GUILayout.EndVertical();
            }
            if (!string.IsNullOrEmpty(lastResult)) GUILayout.Label(lastResult);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
