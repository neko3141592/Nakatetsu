using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims
{
    [DisallowMultipleComponent]
    public sealed class TimsRoot : MonoBehaviour, ITimsMasterBusSource
    {
        public static readonly TimsTagKey BrakeTargetDecelerationsMps2Key =
            new("Brake", "TargetDecelerationsMps2");
        public static readonly TimsTagKey BrakeSubstepCountKey =
            new("Brake", "SubstepCount");
        public static readonly TimsTagKey MaximumServiceBrakeStepKey =
            new("Brake", "MaximumServiceBrakeStep");

        [SerializeField] private TimsSettingsAsset settings;
        private TimsBusState publishedBus;

        [Header("Monitor Output")]
        [SerializeField] private GameObject monitorOutputPrefab;
        [SerializeField] private Vector2Int monitorResolution = new Vector2Int(1536, 1024);

        private GameObject monitorOutputContainer;
        // 配列の0番が画面番号1に対応する。RTの所有者はこのTIMS。
        private RenderTexture[] monitorTextures;
        private bool monitorOutputReady;

        public TimsSettingsAsset Settings => settings;
        public bool HasSettings => settings != null;

        private void Awake()
        {
            CreateMonitorOutput();
        }

        private void OnEnable()
        {
            if (monitorOutputReady && monitorOutputContainer != null) monitorOutputContainer.SetActive(true);
        }

        private void OnDisable()
        {
            if (monitorOutputContainer != null) monitorOutputContainer.SetActive(false);
            if (publishedBus != null) ClearBrakeSettings(publishedBus);
        }

        public void WriteTimsBus(TimsBusState masterBus)
        {
            if (publishedBus != masterBus)
            {
                if (publishedBus != null) ClearBrakeSettings(publishedBus);
                publishedBus = masterBus;
            }
            if (masterBus == null) return;

            if (!isActiveAndEnabled || settings == null || settings.brakeNotchCount <= 0 ||
                settings.brakeSubstepCount <= 0 || settings.brakeTargetDecelerationsKmhPerSec == null ||
                settings.brakeTargetDecelerationsKmhPerSec.Count != settings.brakeNotchCount)
            {
                ClearBrakeSettings(masterBus);
                return;
            }

            // B1が1step。常用最大には、その通常ノッチの刻み0が対応する。
            long maximumServiceBrakeStep = (long)(settings.brakeNotchCount - 1) *
                settings.brakeSubstepCount + 1;
            if (maximumServiceBrakeStep > int.MaxValue)
            {
                ClearBrakeSettings(masterBus);
                return;
            }

            var decelerationsMps2 = new float[settings.brakeNotchCount];
            for (int i = 0; i < decelerationsMps2.Length; i++)
            {
                float value = settings.brakeTargetDecelerationsKmhPerSec[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    ClearBrakeSettings(masterBus);
                    return;
                }
                decelerationsMps2[i] = value / 3.6f;
            }

            // 共通設定をMaster収集周期で公開する。配列はBus側でコピーされる。
            masterBus.SetFloatArray(BrakeTargetDecelerationsMps2Key, decelerationsMps2);
            masterBus.SetInt(BrakeSubstepCountKey, settings.brakeSubstepCount);
            masterBus.SetInt(MaximumServiceBrakeStepKey, (int)maximumServiceBrakeStep);
        }

        private static void ClearBrakeSettings(TimsBusState masterBus)
        {
            masterBus.Remove(BrakeTargetDecelerationsMps2Key);
            masterBus.Remove(BrakeSubstepCountKey);
            masterBus.Remove(MaximumServiceBrakeStepKey);
        }

        public bool TryGetMonitorTexture(int screenNumber, out RenderTexture texture)
        {
            texture = null;
            int index = screenNumber - 1;
            if (monitorTextures == null || index < 0 || index >= monitorTextures.Length) return false;
            texture = monitorTextures[index];
            return texture != null && texture.IsCreated();
        }

        private void CreateMonitorOutput()
        {
            if (monitorOutputPrefab == null || monitorOutputContainer != null) return;
            var definition = monitorOutputPrefab.GetComponent<ITimsMonitorOutput>();
            if (definition == null || definition.ScreenCount == 0)
            {
                Debug.LogError("Monitor Output Prefabに画面を設定したTimsMonitorOutputが必要です。", this);
                return;
            }

            // UIのAwakeより先に、描画先と接続先のTIMSを設定する。
            monitorOutputContainer = new GameObject("Monitor Output");
            monitorOutputContainer.SetActive(false);
            monitorOutputContainer.transform.SetParent(transform, false);
            var instance = Instantiate(monitorOutputPrefab, monitorOutputContainer.transform, false);
            instance.name = monitorOutputPrefab.name;

            monitorTextures = new RenderTexture[definition.ScreenCount];
            for (int index = 0; index < monitorTextures.Length; index++)
            {
                var texture = new RenderTexture(Mathf.Max(1, monitorResolution.x),
                    Mathf.Max(1, monitorResolution.y), 24, RenderTextureFormat.ARGB32)
                {
                    name = $"{name} Monitor {index + 1} (Runtime)",
                    hideFlags = HideFlags.DontSave,
                    useMipMap = true,
                    autoGenerateMips = true,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                monitorTextures[index] = texture;
                if (!texture.Create())
                {
                    Debug.LogError($"画面{index + 1}のRenderTextureを生成できませんでした。", this);
                    return;
                }
            }

            instance.GetComponent<ITimsMonitorOutput>().Initialize(
                GetComponent<TimsCommunicationController>(), monitorTextures);
            monitorOutputReady = true;
            monitorOutputContainer.SetActive(isActiveAndEnabled);
        }

        private void OnDestroy()
        {
            if (monitorOutputContainer != null)
            {
                foreach (var camera in monitorOutputContainer.GetComponentsInChildren<Camera>(true))
                    camera.targetTexture = null;
                if (UnityEngine.Application.isPlaying) Destroy(monitorOutputContainer);
                else DestroyImmediate(monitorOutputContainer);
            }
            if (monitorTextures == null) return;
            foreach (var texture in monitorTextures)
            {
                if (texture == null) continue;
                texture.Release();
                if (UnityEngine.Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }
            monitorTextures = null;
        }

        public void Configure(TimsSettingsAsset value)
        {
            // TIMS全体で使用する設定アセットを割り当てる。
            settings = value;
            if (publishedBus != null) ClearBrakeSettings(publishedBus);
        }
    }
}
