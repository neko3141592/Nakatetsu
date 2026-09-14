using Nakatetsu.Train.Equipment.Shared;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class EbDevice : MonoBehaviour, IEquipmentController
    {
        [SerializeField] private MonoBehaviour masterControllerInputSource;
        [SerializeField, Min(0f)] private float activationDelaySeconds = 60f;

        private readonly EbDeviceContext context = new();
        private IEbMasterControllerInputSource resolvedInputSource;

        public EbDeviceContext Context => context;
        public EbDeviceOutput Output => context.Output;
        public bool HasOutput { get; private set; }
        public bool IsEmergencyBrakeRequested => context.Output.isEmergencyBrakeRequested;

        private void Awake()
        {
            // TIMS経由でマスコン状態を提供する入力元を解決する。
            ResolveInputSource();
        }

        public void CollectInput()
        {
            // TIMS経由の有効運転台判定と自車のマスコン状態をContextへコピーする。
            EbDeviceInput input = context.Input;
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
            context.Settings.activationDelaySeconds = Mathf.Max(0f, activationDelaySeconds);
            EbDeviceLogic.Calculate(context, deltaTimeSeconds);
            HasOutput = true;
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 計算済みOutputはTIMS側のBusSourceが次の収集時に公開する。
            // 実際の非常ブレーキへの反映は行わない。
        }

        public void Configure(IEbMasterControllerInputSource inputSource, float delaySeconds)
        {
            // 使用するTIMS入力元とEB作動時間を設定する。
            resolvedInputSource = inputSource;
            masterControllerInputSource = inputSource as MonoBehaviour;
            activationDelaySeconds = Mathf.Max(0f, delaySeconds);
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
            activationDelaySeconds = Mathf.Max(0f, activationDelaySeconds);
        }
    }
}
