using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Monitors
{
    [DisallowMultipleComponent]
    public sealed class TimsMonitorScreen : MonoBehaviour
    {
        [SerializeField] private TimsRoot tims;
        [SerializeField, Min(1)] private int screenNumber = 1;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField, Min(0)] private int materialIndex;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        private MaterialPropertyBlock originalProperties;
        private MaterialPropertyBlock screenProperties;
        private RenderTexture boundTexture;

        private void OnEnable()
        {
            if (screenRenderer == null) screenRenderer = GetComponent<Renderer>();
            if (tims == null) tims = GetComponentInParent<TimsRoot>();
        }

        private void LateUpdate()
        {
            if (tims == null || screenRenderer == null) return;
            if (!tims.TryGetMonitorTexture(screenNumber, out var texture)) return;
            if (boundTexture == texture) return;

            var materials = screenRenderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null) return;
            if (originalProperties == null)
            {
                originalProperties = new MaterialPropertyBlock();
                screenProperties = new MaterialPropertyBlock();
                screenRenderer.GetPropertyBlock(originalProperties, materialIndex);
            }
            screenRenderer.GetPropertyBlock(screenProperties, materialIndex);
            // 共有Materialアセットを変更せず、このモニターだけにRTを設定する。
            if (materials[materialIndex].HasProperty(BaseMap)) screenProperties.SetTexture(BaseMap, texture);
            if (materials[materialIndex].HasProperty(EmissionMap)) screenProperties.SetTexture(EmissionMap, texture);
            screenRenderer.SetPropertyBlock(screenProperties, materialIndex);
            boundTexture = texture;
        }

        private void OnDisable()
        {
            if (screenRenderer != null && originalProperties != null)
                screenRenderer.SetPropertyBlock(originalProperties, materialIndex);
            originalProperties = null;
            screenProperties = null;
            boundTexture = null;
        }
    }
}
