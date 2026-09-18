using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Door;
using UnityEngine;

namespace Nakatetsu.Train.Debugging
{
    // 試作用。TIMSのページ遷移や運転台の正式な操作権限を実装するものではない。
    public sealed class DoorDebugPanel : MonoBehaviour
    {
        [SerializeField] private Rect panelRect = new(20, 400, 540, 360);
        private Vector2 scroll;

        [ContextMenu("Install doors (Play Mode only)")]
        public void InstallDoors()
        {
            if (!Application.isPlaying) return;
            var root = GetComponentInParent<TrainRoot>();
            if (root == null || root.ConsistDefinition == null) return;
            for (int i = 0; i < root.ConsistDefinition.CarCount; i++)
            {
                bool exists = false;
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>(true))
                    if (door.GetComponent<TrainEquipmentAssignment>().AssignedCarIndex == i) exists = true;
                if (!exists)
                {
                    var go = new GameObject($"Door_Car_{i + 1}");
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<DoorController>();
                    go.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(i);
                    go.AddComponent<DoorTimsInputAdapter>();
                    go.AddComponent<DoorTimsBusSource>();
                }
                bool hasSimulation = false;
                foreach (TrainDoorSimulation door in root.GetComponentsInChildren<TrainDoorSimulation>(true))
                {
                    var assignment = door.GetComponentInParent<TrainSimulationAssignment>();
                    if (assignment != null && assignment.AssignedCarIndex == i) hasSimulation = true;
                }
                if (!hasSimulation)
                {
                    var go = new GameObject($"DoorSimulation_Car_{i + 1}");
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<TrainSimulationAssignment>().AssignCarIndex(i);
                    go.AddComponent<TrainDoorSimulation>();
                }
            }
            var simulation = root.GetComponentInChildren<TrainSimulationController>();
            if (simulation != null) simulation.ResolveReferences();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            var root = GetComponentInParent<TrainRoot>();
            if (root == null) return;
            GUILayout.BeginArea(panelRect, "Door debug", GUI.skin.window);
            if (GUILayout.Button("Install doors on all cars")) InstallDoors();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open left"))
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>()) door.OpenLeft();
            if (GUILayout.Button("Open right"))
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>()) door.OpenRight();
            if (GUILayout.Button("Close both"))
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>()) door.CloseBoth();
            GUILayout.EndHorizontal();
            var communication = root.GetComponentInChildren<TimsCommunicationController>();
            if (communication == null || !communication.isActiveAndEnabled)
                GUILayout.Label("TIMS unavailable");
            else
            {
                var bus = communication.MasterBus;
                bool valid = bus.TryGetBool(TimsDoorController.HasValidStateKey, out bool v) && v;
                bool closed = bus.TryGetBool(TimsDoorController.AllClosedKey, out bool c) && c;
                bool permitted = bus.TryGetBool(TimsDoorController.TractionPermittedKey, out bool p) && p;
                GUILayout.Label($"Data: {(valid ? "valid" : "unknown")} / All closed: {closed} / Traction: {permitted}");
                GUILayout.Label("0 Closed / 1 Opening / 2 Open / 3 Closing / 4 Stopped / 5 Fault");
                scroll = GUILayout.BeginScrollView(scroll);
                int count = root.ConsistDefinition != null ? root.ConsistDefinition.CarCount : 0;
                for (int i = 0; i < count; i++)
                {
                    if (!communication.TryGetLocalBus(i, out TimsBusState local)) continue;
                    string left = local.TryGetIntArray(DoorTimsBusSource.LeftStatusKey, out int[] l) ? string.Join(" ", l) : "unknown";
                    string right = local.TryGetIntArray(DoorTimsBusSource.RightStatusKey, out int[] r) ? string.Join(" ", r) : "unknown";
                    GUILayout.Label($"Car {i + 1}   L: {left}   R: {right}");
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
    }
}
