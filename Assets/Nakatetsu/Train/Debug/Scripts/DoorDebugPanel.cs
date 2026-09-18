using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Train.Debugging
{
    // 試作用。TIMSのページ遷移や運転台の正式な操作権限を実装するものではない。
    public sealed class DoorDebugPanel : MonoBehaviour
    {
        [SerializeField] private Rect panelRect = new(20, 400, 540, 360);
        private Vector2 scroll;

        [ContextMenu("Install four-door prototype (Play Mode only)")]
        public void InstallPrototype()
        {
            if (!Application.isPlaying) return;
            var root = GetComponentInParent<TrainRoot>();
            if (root == null || root.ConsistDefinition == null) return;
            for (int i = 0; i < root.ConsistDefinition.CarCount; i++)
            {
                bool exists = false;
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>(true))
                    if (door.GetComponent<TrainEquipmentAssignment>().AssignedCarIndex == i) exists = true;
                if (exists) continue;
                var go = new GameObject($"DoorPrototype_Car_{i + 1}");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<DoorController>();
                go.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(i);
                go.AddComponent<DoorTimsInputAdapter>();
                go.AddComponent<DoorTimsBusSource>();
            }
            var simulation = root.GetComponentInChildren<TrainSimulationController>();
            if (simulation != null) simulation.ResolveReferences();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            var root = GetComponentInParent<TrainRoot>();
            if (root == null) return;
            GUILayout.BeginArea(panelRect, "Door prototype — 4 per side", GUI.skin.window);
            if (GUILayout.Button("Install prototype on all cars")) InstallPrototype();
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
