using System.Collections.Generic;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
using UnityEngine;

namespace Nakatetsu.Track.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Debug/Track Interlocking Debug Display")]
    public sealed class TrackInterlockingDebugDisplay : MonoBehaviour
    {
        [SerializeField] private TrackInterlockingController interlocking;
        [SerializeField] private TrackCircuitSimulationController circuits;
        [SerializeField] private bool showInGameView = true;

        private Vector2 scrollPosition;
        private string lastResult;

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying || !showInGameView)
            {
                return;
            }

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
            foreach (string routeId in interlocking.RouteIds)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(routeId);

                if (interlocking.TryGetRouteStatus(routeId, out var status))
                {
                    GUILayout.Label($"Set: {status.IsRouteSet}  Proceed: {status.ProceedAllowed}  Route lock: {status.RouteLocked}");
                    GUILayout.Label($"Path: {status.PathEstablished}  Overrun: {status.OverrunMode} / {status.OverrunPhase}");
                    GUILayout.Label($"Overrun release: {status.OverrunReleaseRemainingSeconds:F1}s");
                    GUILayout.Label($"Cancel: {status.CancelPending}  Approach lock: {status.ApproachLocked}  {status.ApproachReleaseRemainingSeconds:F1}s");

                    var passages = new List<string>();
                    foreach (var pair in status.CircuitPassageById)
                    {
                        bool occupied = circuits == null || circuits.IsOccupied(pair.Key);
                        passages.Add($"{pair.Key}:{(occupied ? "OCC" : "CLR")}/{pair.Value}");
                    }
                    GUILayout.Label(string.Join("  ", passages));
                }
                else
                {
                    GUILayout.Label("Unavailable");
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Request"))
                {
                    lastResult = interlocking.TryRequestRoute(routeId, out string requestError)
                        ? $"{routeId}: requested" : requestError;
                }
                if (GUILayout.Button("Cancel"))
                {
                    lastResult = interlocking.TryCancelRoute(routeId, out string cancelError)
                        ? $"{routeId}: cancel accepted" : cancelError;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            if (!string.IsNullOrEmpty(lastResult))
            {
                GUILayout.Label(lastResult);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
