using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Connection;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Editor
{
    [CustomEditor(typeof(TrackConnectionController))]
    public sealed class TrackConnectionControllerEditor : UnityEditor.Editor
    {
        private string lastError;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Switch controls are available in Play Mode.", MessageType.Info);
                return;
            }

            var connections = (TrackConnectionController)target;
            var graph = connections.GetComponent<TrackGraphController>();
            if (!connections.IsInitialized || graph == null || !graph.IsInitialized)
            {
                EditorGUILayout.HelpBox("Track connections are not initialized.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Switch debug controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Debug requests go directly to the switch. Occupancy and route locking will be checked by interlocking in W2-03.",
                MessageType.Info);
            foreach (var definition in graph.Context.Connections)
            {
                if (definition.edgePairs == null || definition.edgePairs.Count != 2 ||
                    !connections.TryGetState(definition.connectionId, out var state))
                {
                    continue;
                }

                EditorGUILayout.LabelField(definition.connectionId);
                EditorGUILayout.LabelField("Position", state.IsMoving
                    ? $"Moving to {state.RequestedPosition} (undetected)"
                    : state.ActualPosition.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(state.IsMoving ||
                        state.ActualPosition == TrackSwitchPosition.Normal))
                    {
                        if (GUILayout.Button("Normal"))
                        {
                            Request(connections, definition.connectionId, TrackSwitchPosition.Normal);
                        }
                    }

                    using (new EditorGUI.DisabledScope(state.IsMoving ||
                        state.ActualPosition == TrackSwitchPosition.Reverse))
                    {
                        if (GUILayout.Button("Reverse"))
                        {
                            Request(connections, definition.connectionId, TrackSwitchPosition.Reverse);
                        }
                    }

                    if (state.IsMoving && GUILayout.Button("Confirm"))
                    {
                        lastError = connections.TryConfirmPosition(definition.connectionId,
                            state.RequestedPosition, out var error) ? null : error;
                        Repaint();
                    }
                }
            }

            if (!string.IsNullOrEmpty(lastError))
            {
                EditorGUILayout.HelpBox(lastError, MessageType.Warning);
            }
        }

        private void Request(TrackConnectionController connections, string connectionId,
            TrackSwitchPosition position)
        {
            lastError = connections.TryRequestPosition(connectionId, position, out var error)
                ? null : error;
            Repaint();
        }
    }
}
