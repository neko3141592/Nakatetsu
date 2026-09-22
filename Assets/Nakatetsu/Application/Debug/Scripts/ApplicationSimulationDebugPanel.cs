using Nakatetsu.Application.Simulation;
using UnityEngine;

namespace Nakatetsu.Application.Debugging
{
    // 開発用の表示と操作。時間進行はApplicationSimulationControllerに任せる。
    public sealed class ApplicationSimulationDebugPanel : MonoBehaviour
    {
        [SerializeField] private ApplicationSimulationController simulation;
        [SerializeField] private Rect panelRect = new(580, 20, 280, 160);

        private void Reset()
        {
            simulation = GetComponent<ApplicationSimulationController>();
        }

        private void Awake()
        {
            if (simulation == null)
                simulation = GetComponent<ApplicationSimulationController>();
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying) return;

            GUILayout.BeginArea(panelRect, "Simulation debug", GUI.skin.window);
            if (simulation == null)
            {
                GUILayout.Label("Simulationを設定してください。");
            }
            else
            {
                // 内部時刻はそのまま保ち、表示のみ24時間表記にする。
                long seconds = (long)simulation.WorldTimeSeconds;
                GUILayout.Label($"世界時刻: {seconds / 3600 % 24:00}:{seconds / 60 % 60:00}:{seconds % 60:00}");
                GUILayout.Label($"再生倍率: ×{simulation.PlaybackSpeed:0.##}");
                GUILayout.Label(simulation.IsPaused ? "一時停止" : "実行中");
                if (GUILayout.Button(simulation.IsPaused ? "再開" : "一時停止"))
                    simulation.SetPaused(!simulation.IsPaused);
            }
            GUILayout.EndArea();
        }
    }
}
