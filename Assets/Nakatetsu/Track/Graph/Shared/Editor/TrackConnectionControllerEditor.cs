using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Connection;
using UnityEditor;

namespace Nakatetsu.Track.Graph.Editor
{
    [CustomEditor(typeof(TrackConnectionController))]
    public sealed class TrackConnectionControllerEditor : UnityEditor.Editor
    {
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
            EditorGUILayout.LabelField("Switch states", EditorStyles.boldLabel);
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
            }
        }
    }
}
