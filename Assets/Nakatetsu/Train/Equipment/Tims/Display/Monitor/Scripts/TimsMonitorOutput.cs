using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using Nakatetsu.Train.Presentation.Gauges;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Monitors
{
    [DisallowMultipleComponent]
    public sealed class TimsMonitorOutput : MonoBehaviour, ITimsMonitorOutput
    {
        [Tooltip("画面番号1、2、3の順に描画用Cameraを設定します。")]
        [SerializeField] private Camera[] screenCameras = Array.Empty<Camera>();

        private static readonly HashSet<int> occupiedRenderSlots = new HashSet<int>();
        private int renderSlot = -1;
        private Vector3 renderPosition;

        public int ScreenCount => screenCameras.Length;

        public void Initialize(TimsCommunicationController source, IReadOnlyList<RenderTexture> textures)
        {
            if (renderSlot < 0)
            {
                renderSlot = 0;
                while (occupiedRenderSlots.Contains(renderSlot)) renderSlot++;
                occupiedRenderSlots.Add(renderSlot);
                // 同じLayerを使う別編成のCanvasがCameraに重ならないよう、描画空間を分ける。
                renderPosition = new Vector3(renderSlot * 100f, -2000f, 0f);
            }
            UpdateRenderPosition();
            for (int index = 0; index < screenCameras.Length; index++)
            {
                if (screenCameras[index] == null)
                {
                    Debug.LogError($"画面{index + 1}のCameraが未設定です。", this);
                    continue;
                }
                screenCameras[index].targetTexture = textures[index];
            }

            foreach (var display in GetComponentsInChildren<TimsSpeedMeter>(true)) display.SetTimsSource(source);
            foreach (var display in GetComponentsInChildren<TimsNotchDisplay>(true)) display.Configure(source);
            foreach (var display in GetComponentsInChildren<TimsPressureGauge>(true)) display.SetTimsSource(source);
            foreach (var display in GetComponentsInChildren<TimsCurrentGauge>(true)) display.SetTimsSource(source);
            foreach (var display in GetComponentsInChildren<TimsBoolIndicatorDisplay>(true)) display.SetTimsSource(source);
        }

        private void LateUpdate()
        {
            if (renderSlot >= 0) UpdateRenderPosition();
        }

        private void UpdateRenderPosition()
        {
            // 列車が移動・回転しても、描画用CameraとCanvasの位置を固定する。
            transform.SetPositionAndRotation(renderPosition, Quaternion.identity);
        }

        private void OnDestroy()
        {
            if (renderSlot >= 0) occupiedRenderSlots.Remove(renderSlot);
        }
    }
}
