using System.Collections.Generic;
using UnityEngine;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Equipment.Traction.Motor;

namespace Nakatetsu.Train.Equipment.Traction.Vvvf
{
    [DisallowMultipleComponent]
    public sealed class VvvfController : MonoBehaviour, ITractionEquipment
    {
        [Header("Definitions")]
        [SerializeField] private VvvfDefinitionAsset definition;
        [SerializeField] private TrainDriveDefinition driveDefinition;

        [Header("Child Motors (auto resolved)")]
        [SerializeField] private List<MotorController> motors = new();

        [Header("Unit Variation")]
        [SerializeField, Range(0.9f, 1.1f)] private float responseVariation = 1f;

        private readonly VvvfContext context = new();
        private VvvfDefinitionAsset appliedDefinition;
        private float totalMotorTractionForceN;
        private float totalMotorCurrentRmsA;
        private float totalMotorOutputPowerW;
        private ITractionCommandSource tractionCommandSource;

        public VvvfDefinitionAsset Definition => definition;
        public TrainDriveDefinition DriveDefinition => driveDefinition;
        public IReadOnlyList<MotorController> Motors => motors;
        public int MotorCount => CountValidMotors();
        public VvvfState State => context.State;
        public VvvfOutput Output => context.Output;
        public bool IsAvailable =>
            definition != null && driveDefinition != null && GetRepresentativeMotor() != null;
        public float TargetTractionForceN => context.Input.targetTractionForceN;
        public float ActualTractionForceN => totalMotorTractionForceN;
        public float TotalMotorTractionForceN => totalMotorTractionForceN;
        public float TotalMotorCurrentRmsA => totalMotorCurrentRmsA;
        public float TotalMotorOutputPowerW => totalMotorOutputPowerW;
        public float RatedPowerW
        {
            get
            {
                float total = 0f;
                foreach (MotorController motor in motors)
                {
                    if (motor != null) total += motor.RatedPowerW;
                }
                return total;
            }
        }

        private void Awake()
        {
            RefreshMotors();
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
            ReadTargetTractionForce();
            ApplyDefinition();
            MotorController representativeMotor = GetRepresentativeMotor();
            if (appliedDefinition == null || driveDefinition == null || representativeMotor == null)
            {
                ResetDrive();
                return;
            }

            PopulateInput(representativeMotor, deltaTimeSeconds);
            VvvfLogic.Calculate(context);
            StepMotors();
            AggregateMotorOutput();
        }

        public float GetRegenCapacityN(float vehicleSpeedMps)
        {
            if (definition == null || driveDefinition == null || MotorCount == 0) return 0f;

            VvvfSettings settings = context.Settings;
            float totalRatedTorqueNm = 0f;
            float totalRatedPowerW = 0f;
            foreach (MotorController motor in motors)
            {
                if (motor == null) continue;
                MotorSettings motorSettings = motor.Settings;
                totalRatedPowerW += motorSettings.ratedPowerW;
                totalRatedTorqueNm += MotorLogic.GetTorqueFromPowerAndRpm(
                    motorSettings.ratedPowerW,
                    motorSettings.ratedRpm);
            }

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

        public void RefreshMotors()
        {
            motors.Clear();
            motors.AddRange(GetComponentsInChildren<MotorController>(true));
        }

        public void ResetDrive()
        {
            context.Input.deltaTimeSeconds = 0f;
            context.Input.vehicleSpeedMps = 0f;
            context.Input.targetTractionForceN = 0f;
            context.State.slipFrequencyHz = 0f;
            context.State.voltageRatio = 0f;
            context.State.phaseRad = 0f;
            context.Output.Reset();
            foreach (MotorController motor in motors)
            {
                if (motor != null) motor.ResetMotor();
            }
            totalMotorTractionForceN = 0f;
            totalMotorCurrentRmsA = 0f;
            totalMotorOutputPowerW = 0f;
        }

        public void ResetEquipment()
        {
            ResetDrive();
        }

        private void PopulateInput(
            MotorController representativeMotor,
            float deltaTimeSeconds)
        {
            MotorSettings motorSettings = representativeMotor.Settings;
            VvvfInput input = context.Input;
            context.State.responseVariation = responseVariation;
            input.deltaTimeSeconds = Mathf.Max(0f, deltaTimeSeconds);
            input.wheelRadiusM = driveDefinition.wheelRadiusM;
            input.gearRatio = driveDefinition.gearRatio;
            input.transmissionEfficiency = driveDefinition.transmissionEfficiency;
            input.motorCount = MotorCount;
            input.representativeMotorTorqueNm = GetAverageMotorTorqueNm();
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

        private void StepMotors()
        {
            foreach (MotorController motor in motors)
            {
                if (motor == null) continue;
                motor.Step(
                    context.Output.lineVoltageRmsV,
                    context.Output.frequencyHz,
                    context.Output.motorRpm);
            }
        }

        private void AggregateMotorOutput()
        {
            totalMotorTractionForceN = 0f;
            totalMotorCurrentRmsA = 0f;
            totalMotorOutputPowerW = 0f;
            foreach (MotorController motor in motors)
            {
                if (motor == null) continue;
                MotorOutput output = motor.Output;
                totalMotorTractionForceN += output.motorTorqueNm *
                    driveDefinition.gearRatio * driveDefinition.transmissionEfficiency /
                    Mathf.Max(0.01f, driveDefinition.wheelRadiusM);
                totalMotorCurrentRmsA += output.motorCurrentRmsA;
                totalMotorOutputPowerW += output.motorOutputPowerW;
            }
        }

        private MotorController GetRepresentativeMotor()
        {
            foreach (MotorController motor in motors)
            {
                if (motor != null && motor.Definition != null) return motor;
            }
            return null;
        }

        private float GetAverageMotorTorqueNm()
        {
            float totalTorqueNm = 0f;
            int count = 0;
            foreach (MotorController motor in motors)
            {
                if (motor == null) continue;
                totalTorqueNm += motor.MotorTorqueNm;
                count++;
            }
            return count > 0 ? totalTorqueNm / count : 0f;
        }

        private int CountValidMotors()
        {
            int count = 0;
            foreach (MotorController motor in motors)
            {
                if (motor != null) count++;
            }
            return count;
        }

        private void ApplyDefinition()
        {
            if (definition == appliedDefinition) return;
            appliedDefinition = definition;
            appliedDefinition?.ApplyTo(context.Settings);
        }

        private void OnValidate()
        {
            RefreshMotors();
            responseVariation = Mathf.Clamp(responseVariation, 0.9f, 1.1f);
            if (Application.isPlaying)
            {
                appliedDefinition = null;
                ApplyDefinition();
            }
        }
    }
}
