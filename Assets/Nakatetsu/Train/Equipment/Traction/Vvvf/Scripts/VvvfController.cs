using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Simulation.Traction.Motor;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Vvvf
{
    [DisallowMultipleComponent]
    public sealed class VvvfController : MonoBehaviour, ITractionEquipment, IEquipmentController
    {
        [Header("Definitions")]
        [SerializeField] private VvvfDefinitionAsset definition;
        [SerializeField] private TrainDriveDefinition driveDefinition;
        [SerializeField] private MotorDefinitionAsset motorDefinition;
        [SerializeField, Min(0)] private int motorCount = 4;

        [Header("Unit Variation")]
        [SerializeField, Range(0.9f, 1.1f)] private float responseVariation = 1f;

        private readonly VvvfContext context = new();
        private VvvfDefinitionAsset appliedDefinition;
        private ITractionCommandSource tractionCommandSource;
        private float measuredMotorTorqueNm;
        private float measuredTractionForceN;
        private float measuredMotorCurrentRmsA;
        private float measuredMotorOutputPowerW;

        public VvvfDefinitionAsset Definition => definition;
        public TrainDriveDefinition DriveDefinition => driveDefinition;
        public MotorDefinitionAsset MotorDefinition => motorDefinition;
        public int MotorCount => Mathf.Max(0, motorCount);
        public VvvfState State => context.State;
        public VvvfOutput Output => context.Output;
        public bool IsAvailable =>
            definition != null && driveDefinition != null && motorDefinition != null && MotorCount > 0;
        public float TargetTractionForceN => context.Input.targetTractionForceN;

        // センサーを兼ねるMVPとして、Simulationから受け取った測定値を公開する。
        public float ActualTractionForceN => measuredTractionForceN;
        public float TotalMotorCurrentRmsA => measuredMotorCurrentRmsA;
        public float TotalMotorOutputPowerW => measuredMotorOutputPowerW;
        public float RatedPowerW => motorDefinition != null
            ? motorDefinition.Settings.ratedPowerW * MotorCount
            : 0f;

        private void Awake()
        {
            ResolveTractionCommandSource();
            ApplyDefinition();
            context.State.responseVariation = responseVariation;
        }

        public void Configure(
            VvvfDefinitionAsset newDefinition,
            TrainDriveDefinition newDriveDefinition)
        {
            definition = newDefinition;
            driveDefinition = newDriveDefinition;
            appliedDefinition = null;
            ApplyDefinition();
        }

        public void ConfigureMotor(
            MotorDefinitionAsset newMotorDefinition,
            TrainDriveDefinition newDriveDefinition,
            int newMotorCount)
        {
            motorDefinition = newMotorDefinition;
            driveDefinition = newDriveDefinition;
            motorCount = Mathf.Max(0, newMotorCount);
        }

        public void SetMotorMeasurement(
            float averageTorqueNm,
            float tractionForceN,
            float totalCurrentRmsA,
            float totalOutputPowerW)
        {
            measuredMotorTorqueNm = averageTorqueNm;
            measuredTractionForceN = tractionForceN;
            measuredMotorCurrentRmsA = Mathf.Max(0f, totalCurrentRmsA);
            measuredMotorOutputPowerW = totalOutputPowerW;
        }

        public void SetTargetTractionForceN(float value)
        {
            context.Input.targetTractionForceN = value;
        }

        public void SetVehicleSpeedMps(float value)
        {
            context.Input.vehicleSpeedMps = value;
        }

        public void Step(float deltaTimeSeconds)
        {
            CollectInput();
            Calculate(deltaTimeSeconds);
            ApplyOutput(deltaTimeSeconds);
        }

        public void CollectInput()
        {
            ReadTargetTractionForce();
        }

        public void Calculate(float deltaTimeSeconds)
        {
            ApplyDefinition();
            if (!IsAvailable)
            {
                ResetControlOutput();
                return;
            }

            PopulateInput(deltaTimeSeconds);
            VvvfLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 物理モデルはTrainMotorSimulationが進める。Equipmentは指令値だけを公開する。
        }

        public float GetRegenCapacityN(float vehicleSpeedMps)
        {
            if (!IsAvailable) return 0f;

            VvvfSettings settings = context.Settings;
            MotorSettings motorSettings = motorDefinition.Settings;
            float totalRatedTorqueNm = MotorLogic.GetTorqueFromPowerAndRpm(
                motorSettings.ratedPowerW,
                motorSettings.ratedRpm) * MotorCount;
            float totalRatedPowerW = motorSettings.ratedPowerW * MotorCount;
            float torqueCapacityN = totalRatedTorqueNm * settings.regenTorqueMultiplier *
                driveDefinition.gearRatio * driveDefinition.transmissionEfficiency /
                Mathf.Max(0.01f, driveDefinition.wheelRadiusM);
            float powerCapacityN = totalRatedPowerW * settings.regenPowerMultiplier /
                Mathf.Max(Mathf.Abs(vehicleSpeedMps), settings.minimumRegenPowerSpeedMps);
            float lowSpeedFactor = Mathf.InverseLerp(
                settings.regenCutOutSpeedMps,
                settings.fullRegenSpeedMps,
                Mathf.Abs(vehicleSpeedMps));

            return Mathf.Max(0f, Mathf.Min(torqueCapacityN, powerCapacityN) * lowSpeedFactor);
        }

        public void ResetEquipment()
        {
            ResetControlOutput();
            SetMotorMeasurement(0f, 0f, 0f, 0f);
        }

        private void PopulateInput(float deltaTimeSeconds)
        {
            MotorSettings motorSettings = motorDefinition.Settings;
            VvvfInput input = context.Input;
            context.State.responseVariation = responseVariation;
            input.deltaTimeSeconds = Mathf.Max(0f, deltaTimeSeconds);
            input.wheelRadiusM = driveDefinition.wheelRadiusM;
            input.gearRatio = driveDefinition.gearRatio;
            input.transmissionEfficiency = driveDefinition.transmissionEfficiency;
            input.motorCount = MotorCount;
            input.representativeMotorTorqueNm = measuredMotorTorqueNm;
            input.ratedMotorLineVoltageV = motorSettings.ratedLineVoltageV;
            input.ratedMotorFrequencyHz = motorSettings.ratedFrequencyHz;
            input.motorPoleCount = motorSettings.poleCount;
        }

        private void ReadTargetTractionForce()
        {
            if (tractionCommandSource == null)
            {
                ResolveTractionCommandSource();
            }

            if (tractionCommandSource != null &&
                tractionCommandSource.TryGetTargetTractionForceN(out float targetTractionForceN))
            {
                SetTargetTractionForceN(targetTractionForceN);
            }
        }

        private void ResolveTractionCommandSource()
        {
            tractionCommandSource = null;
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is ITractionCommandSource commandSource)
                {
                    tractionCommandSource = commandSource;
                    return;
                }
            }
        }

        private void ResetControlOutput()
        {
            context.Input.deltaTimeSeconds = 0f;
            context.State.slipFrequencyHz = 0f;
            context.State.voltageRatio = 0f;
            context.State.phaseRad = 0f;
            context.Output.Reset();
        }

        private void ApplyDefinition()
        {
            if (definition == appliedDefinition) return;
            appliedDefinition = definition;
            appliedDefinition?.ApplyTo(context.Settings);
        }

        private void OnValidate()
        {
            motorCount = Mathf.Max(0, motorCount);
            responseVariation = Mathf.Clamp(responseVariation, 0.9f, 1.1f);
            if (Application.isPlaying)
            {
                appliedDefinition = null;
                ApplyDefinition();
            }
        }
    }
}
