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
        [SerializeField] private MonoBehaviour doorInputSource;
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
            TrackAtcGraphDefinition graph, TrainAtcPosition frontPosition, TrainAtcPosition rearPosition)
        {
            return TrainAtcLogic.TryInitializePosition(context, graph, frontPosition, rearPosition);
        }

        public bool TryCorrectPosition(TrainAtcPosition frontPosition, TrainAtcPosition rearPosition)
        {
            return TrainAtcLogic.TryCorrectPosition(context, frontPosition, rearPosition);
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

        public void CollectInput()
        {
            ClearInput();
            if (!isActiveAndEnabled)
            {
                return;
            }

            var input = context.Input;

            // 工程1。操作・ドア・設定・質量は今回取得した値だけを取り込む。
            input.hasCabState = TryReadCabInput(out input.cab);
            input.hasDoorState = TryReadDoorState(out input.areAllDoorsClosed);
            input.hasBrakeSettings = TryReadBrakeSettings();
            if (TryReadCarInputs() && TryReadReceiverOffsets())
            {
                input.hasCarMasses = true;
            }
            else
            {
                input.cars.Clear();
                input.frontReceiverDistanceFromFrontM = 0f;
                input.rearReceiverDistanceFromFrontM = 0f;
            }

            if (speedSensor != null && speedSensor.TryGetSignedMeasuredSpeedMps(out float speedMps))
            {
                input.hasSpeedMeasurement = true;
                input.signedSpeedMps = speedMps;
            }

            // 受電結果も複製し、CollectInput後に地上電文が更新されても内容を変えない。
            if (frontReceiver != null && frontReceiver.TryGetTelegram(out var frontTelegram))
            {
                input.frontTelegram = frontTelegram?.Clone();
            }
            if (rearReceiver != null && rearReceiver.TryGetTelegram(out var rearTelegram))
            {
                input.rearTelegram = rearTelegram?.Clone();
            }
        }

        public void Calculate(float deltaTimeSeconds)
        {
            if (!isActiveAndEnabled)
            {
                ClearInput();
            }

            context.Input.deltaTimeSeconds = deltaTimeSeconds;
            TrainAtcLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 計算済みのOutputをTIMS側のAdapterが読み取る。
        }

        private void ClearInput()
        {
            var input = context.Input;
            input.hasCabState = false;
            input.cab = default;
            input.hasDoorState = false;
            input.areAllDoorsClosed = false;
            input.hasBrakeSettings = false;
            input.brakeSettings.brakeSubstepCount = 0;
            input.brakeSettings.maximumServiceBrakeStep = 0;
            input.brakeSettings.brakeTargetDecelerationsMps2.Clear();

            input.frontTelegram = null;
            input.rearTelegram = null;
            input.hasSpeedMeasurement = false;
            input.signedSpeedMps = 0f;
            input.deltaTimeSeconds = 0f;

            input.hasCarMasses = false;
            input.cars.Clear();
            input.frontReceiverDistanceFromFrontM = 0f;
            input.rearReceiverDistanceFromFrontM = 0f;
        }

        private bool TryReadCabInput(out TrainAtcCabInput input)
        {
            input = default;
            if (cabInputSource == null)
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcCabInputSource)
                    {
                        cabInputSource = component;
                        break;
                    }
                }
            }

            if (cabInputSource is ITrainAtcCabInputSource source)
            {
                return source.TryReadCabInput(out input);
            }
            return false;
        }

        private bool TryReadDoorState(out bool areAllDoorsClosed)
        {
            areAllDoorsClosed = false;
            if (doorInputSource == null)
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcDoorInputSource)
                    {
                        doorInputSource = component;
                        break;
                    }
                }
            }

            if (doorInputSource is ITrainAtcDoorInputSource source)
            {
                return source.TryReadDoorState(out areAllDoorsClosed);
            }
            return false;
        }

        private bool TryReadCarInputs()
        {
            if (massInputSource == null)
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcMassInputSource)
                    {
                        massInputSource = component;
                        break;
                    }
                }
            }

            if (massInputSource is ITrainAtcMassInputSource source)
            {
                return source.TryReadCarInputs(context.Input.cars);
            }
            return false;
        }

        private bool TryReadBrakeSettings()
        {
            if (brakeSettingsInputSource == null)
            {
                foreach (var component in GetComponents<MonoBehaviour>())
                {
                    if (component is ITrainAtcBrakeSettingsInputSource)
                    {
                        brakeSettingsInputSource = component;
                        break;
                    }
                }
            }

            if (brakeSettingsInputSource is ITrainAtcBrakeSettingsInputSource source)
            {
                return source.TryReadBrakeSettings(context.Input.brakeSettings);
            }
            return false;
        }

        private bool TryReadReceiverOffsets()
        {
            var input = context.Input;
            if (input.cars.Count == 0 || frontReceiver == null || rearReceiver == null)
            {
                return false;
            }

            input.frontReceiverDistanceFromFrontM =
                input.cars[0].centerDistanceFromFrontM - frontReceiver.OffsetFromCarCenterM;
            input.rearReceiverDistanceFromFrontM =
                input.cars[input.cars.Count - 1].centerDistanceFromFrontM - rearReceiver.OffsetFromCarCenterM;
            return true;
        }

        private void OnDisable()
        {
            ClearInput();
            TrainAtcLogic.Calculate(context);
        }

        private void ApplySettings()
        {
            if (settingsAsset != null)
            {
                settingsAsset.ApplyTo(context.Settings);
            }
        }

        private void OnValidate()
        {
            ApplySettings();
        }
    }
}
