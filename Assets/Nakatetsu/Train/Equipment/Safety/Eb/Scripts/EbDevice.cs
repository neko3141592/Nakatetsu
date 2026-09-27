using Nakatetsu.Train.Equipment.Shared;
using UnityEngine;
using UnityEngine.Serialization;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class EbDevice : MonoBehaviour, IEquipmentController
    {
        [SerializeField] private MonoBehaviour masterControllerInputSource;
        [FormerlySerializedAs("activationDelaySeconds")]
        [SerializeField, Min(0f), Tooltip("無操作からブザー開始までの秒数")]
        private float warningDelaySeconds = 60f;
        [SerializeField, Min(0f), Tooltip("ブザー開始から非常作動までの猶予秒数")]
        private float warningDurationSeconds = 5f;
        [SerializeField, Min(0f)] private float activationSpeedMps = 5f / 3.6f;

        private readonly EbDeviceContext context = new();
        private IEbMasterControllerInputSource resolvedInputSource;
        private bool pendingReset;

        public EbDeviceContext Context => context;
        public EbDeviceOutput Output => context.Output;
        public bool HasOutput { get; private set; }
        public bool IsEmergencyBrakeRequested => context.Output.isEmergencyBrakeRequested;
        public bool IsBuzzerRequested => context.Output.isBuzzerRequested;

        private void Awake()
        {
            // TIMS経由でマスコン状態を提供する入力元を解決する。
            ResolveInputSource();
        }

        /// <summary>次の入力収集で一度だけリセット操作を渡す。非常保持の解除はLogicが判断する。</summary>
        public void RequestReset()
        {
            if (isActiveAndEnabled)
            {
                pendingReset = true;
            }
        }

        private void OnDisable()
        {
            pendingReset = false;
            context.Input.resetRequested = false;
        }

        public void CollectInput()
        {
            // TIMS経由の有効運転台判定と自車のマスコン状態をContextへコピーする。
            EbDeviceInput input = context.Input;
            input.resetRequested = pendingReset;
            pendingReset = false;
            if (ResolveInputSource() &&
                resolvedInputSource.TryReadMasterControllerInput(out EbMasterControllerInput value))
            {
                input.hasMasterControllerState = true;
                input.masterController = value;
                return;
            }

            input.hasMasterControllerState = false;
            input.masterController = default;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            // 収集済みのマスコン状態から無操作時間とEB出力を計算する。
            context.Settings.warningDelaySeconds = Mathf.Max(0f, warningDelaySeconds);
            context.Settings.warningDurationSeconds = Mathf.Max(0f, warningDurationSeconds);
            context.Settings.activationSpeedMps = Mathf.Max(0f, activationSpeedMps);
            EbDeviceLogic.Calculate(context, deltaTimeSeconds);
            context.Input.resetRequested = false;
            HasOutput = true;
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 計算済みOutputはTIMS側のBusSourceが次の収集時に公開する。
            // 実際の非常ブレーキへの反映は行わない。
        }

        public void Configure(IEbMasterControllerInputSource inputSource, float delaySeconds, float graceSeconds = 5f)
        {
            // 使用するTIMS入力元とEB作動時間を設定する。
            resolvedInputSource = inputSource;
            masterControllerInputSource = inputSource as MonoBehaviour;
            warningDelaySeconds = Mathf.Max(0f, delaySeconds);
            warningDurationSeconds = Mathf.Max(0f, graceSeconds);
        }

        private bool ResolveInputSource()
        {
            // 同じGameObject上のTIMS入力アダプターを取得する。
            if (resolvedInputSource != null)
            {
                return true;
            }

            if (masterControllerInputSource is IEbMasterControllerInputSource configuredSource)
            {
                resolvedInputSource = configuredSource;
                return true;
            }

            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is IEbMasterControllerInputSource source)
                {
                    masterControllerInputSource = component;
                    resolvedInputSource = source;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            // 入力元を解決し、Inspectorの作動時間を0秒以上に補正する。
            ResolveInputSource();
            warningDelaySeconds = Mathf.Max(0f, warningDelaySeconds);
            warningDurationSeconds = Mathf.Max(0f, warningDurationSeconds);
            activationSpeedMps = Mathf.Max(0f, activationSpeedMps);
        }
    }
}
