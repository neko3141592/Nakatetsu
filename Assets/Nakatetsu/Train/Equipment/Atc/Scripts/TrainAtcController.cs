using Nakatetsu.Track.Atc;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
using Nakatetsu.Train.Simulation.Atc;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Atc
{
    [DisallowMultipleComponent]
    public sealed class TrainAtcController : MonoBehaviour, IEquipmentController
    {
        [SerializeField] private TrainAtcSettingsAsset settingsAsset;
        [SerializeField] private TrainAtcReceiverController frontReceiver;
        [SerializeField] private TrainAtcReceiverController rearReceiver;
        [SerializeField] private MonoBehaviour cabInputSource;
        [SerializeField] private MonoBehaviour massInputSource;
        [SerializeField] private MonoBehaviour brakeSettingsInputSource;
        [SerializeField] private SpeedSensor speedSensor;
        private readonly TrainAtcContext context = new();

        public TrainAtcContext Context => context;

        private void Awake()
        {
            ApplySettings();
        }

        public bool TryInitializePosition(
            TrackAtcGraphDefinition graph,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            return TrainAtcLogic.TryInitializePosition(context, graph, frontPosition, rearPosition);
        }

        public void SetReceivers(TrainAtcReceiverController front, TrainAtcReceiverController rear)
        {
            frontReceiver = front;
            rearReceiver = rear;
            ClearInput();
            TrainAtcLogic.Calculate(context);
        }

        public void SetSpeedSensor(SpeedSensor sensor)
        {
            speedSensor = sensor;
            context.Input.hasSpeedMeasurement = false;
            context.Input.signedSpeedMps = 0f;
        }

        public bool TryCorrectPosition(TrainAtcPosition frontPosition, TrainAtcPosition rearPosition)
        {
            return TrainAtcLogic.TryCorrectPosition(context, frontPosition, rearPosition);
        }

        public void CollectInput()
        {
            ClearInput();
            if (!isActiveAndEnabled) return;

            // 有効運転台の操作状態を値として取り込む。キーが切でも現在の状態を取得する。
            if (TryReadCabInput(out var cab))
            {
                context.Input.hasCabState = true;
                context.Input.cab = cab;
            }

            // TIMSがMasterBusへ公開した、通常ノッチごとの減速度と刻み段数を取り込む。
            context.Input.hasBrakeSettings = TryReadBrakeSettings();

            // 車両定義とTIMSの応荷重測定値を、今回の計算用に取り込む。
            if (TryReadCarInputs() && TryReadReceiverOffsets())
            {
                context.Input.hasCarMasses = true;
            }
            else
            {
                context.Input.cars.Clear();
                context.Input.frontReceiverDistanceFromFrontM = 0f;
                context.Input.rearReceiverDistanceFromFrontM = 0f;
            }

            if (speedSensor != null && speedSensor.TryGetSignedMeasuredSpeedMps(out var signedSpeedMps))
            {
                context.Input.hasSpeedMeasurement = true;
                context.Input.signedSpeedMps = signedSpeedMps;
            }

            // 編成両端の受信機を直接読む。片側が未受信でも、もう片側は独立して取得する。
            if (frontReceiver != null && frontReceiver.TryGetTelegram(out var frontTelegram))
            {
                context.Input.frontTelegram = frontTelegram;
            }
            if (rearReceiver != null && rearReceiver.TryGetTelegram(out var rearTelegram))
            {
                context.Input.rearTelegram = rearTelegram;
            }
        }

        public void Calculate(float deltaTimeSeconds)
        {
            if (!isActiveAndEnabled) ClearInput();
            context.Input.deltaTimeSeconds = deltaTimeSeconds;
            TrainAtcLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 表示情報とブレーキ指令は、TIMS側がContextのOutputから読み取る。
        }

        private void ClearInput()
        {
            context.Input.hasCabState = false;
            context.Input.cab = default;
            context.Input.hasBrakeSettings = false;
            context.Input.brakeSettings.brakeSubstepCount = 0;
            context.Input.brakeSettings.maximumServiceBrakeStep = 0;
            context.Input.brakeSettings.brakeTargetDecelerationsMps2.Clear();
            context.Input.frontTelegram = null;
            context.Input.rearTelegram = null;
            context.Input.hasSpeedMeasurement = false;
            context.Input.signedSpeedMps = 0f;
            context.Input.deltaTimeSeconds = 0f;
            context.Input.hasCarMasses = false;
            context.Input.cars.Clear();
            context.Input.frontReceiverDistanceFromFrontM = 0f;
            context.Input.rearReceiverDistanceFromFrontM = 0f;
        }

        private bool TryReadBrakeSettings()
        {
            if (brakeSettingsInputSource == null)
            {
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcBrakeSettingsInputSource)
                    {
                        brakeSettingsInputSource = component;
                        break;
                    }
                }
            }

            if (brakeSettingsInputSource != null &&
                brakeSettingsInputSource is ITrainAtcBrakeSettingsInputSource source)
            {
                return source.TryReadBrakeSettings(context.Input.brakeSettings);
            }

            return false;
        }

        private bool TryReadCarInputs()
        {
            if (massInputSource == null)
            {
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcMassInputSource)
                    {
                        massInputSource = component;
                        break;
                    }
                }
            }

            if (massInputSource != null && massInputSource is ITrainAtcMassInputSource source)
            {
                return source.TryReadCarInputs(context.Input.cars);
            }

            return false;
        }

        private bool TryReadReceiverOffsets()
        {
            if (context.Input.cars.Count == 0 || frontReceiver == null || rearReceiver == null)
            {
                return false;
            }

            // 受信機の取り付け位置は固定前方向が正。先頭端からの距離へ変換する。
            float frontDistanceM = context.Input.cars[0].centerDistanceFromFrontM - frontReceiver.OffsetFromCarCenterM;
            float rearDistanceM = context.Input.cars[context.Input.cars.Count - 1].centerDistanceFromFrontM - rearReceiver.OffsetFromCarCenterM;
            if (float.IsNaN(frontDistanceM) || float.IsInfinity(frontDistanceM) ||
                float.IsNaN(rearDistanceM) || float.IsInfinity(rearDistanceM))
            {
                return false;
            }

            context.Input.frontReceiverDistanceFromFrontM = frontDistanceM;
            context.Input.rearReceiverDistanceFromFrontM = rearDistanceM;
            return true;
        }

        private bool TryReadCabInput(out TrainAtcCabInput input)
        {
            input = default;
            if (cabInputSource == null)
            {
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcCabInputSource)
                    {
                        cabInputSource = component;
                        break;
                    }
                }
            }

            if (cabInputSource != null && cabInputSource is ITrainAtcCabInputSource source)
            {
                return source.TryReadCabInput(out input);
            }

            return false;
        }

        private void OnDisable()
        {
            ClearInput();
            TrainAtcLogic.Calculate(context);
            ApplyOutput(0f);
        }

        private void ApplySettings()
        {
            if (settingsAsset == null) return;

            // Assetを共有しても、編成ごとのContextには設定値をコピーする。
            settingsAsset.ApplyTo(context.Settings);
        }

        private void OnValidate()
        {
            ApplySettings();
        }
    }
}
