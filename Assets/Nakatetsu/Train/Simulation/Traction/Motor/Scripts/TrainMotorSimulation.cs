using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Traction.Motor
{
    /// <summary>1両分の主電動機の物理状態を所有する。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainMotorSimulation : MonoBehaviour, ISimulationController
    {
        [SerializeField] private MotorDefinitionAsset definition;
        [SerializeField] private TrainDriveDefinition driveDefinition;
        [SerializeField, Min(0)] private int motorCount = 4;

        private readonly List<MotorContext> motors = new();
        private MotorDefinitionAsset appliedDefinition;
        private int appliedMotorCount = -1;
        private float lineVoltageRmsV;
        private float frequencyHz;
        private float vehicleSpeedMps;

        public MotorDefinitionAsset Definition => definition;
        public TrainDriveDefinition DriveDefinition => driveDefinition;
        public IReadOnlyList<MotorContext> Motors => motors;
        public int MotorCount => motors.Count;
        public bool IsAvailable => definition != null && driveDefinition != null && motorCount > 0;
        public float RatedPowerW => definition != null
            ? definition.Settings.ratedPowerW * Mathf.Max(0, motorCount)
            : 0f;
        public float AverageMotorTorqueNm { get; private set; }
        public float ActualTractionForceN { get; private set; }
        public float TotalMotorCurrentRmsA { get; private set; }
        public float TotalMotorOutputPowerW { get; private set; }

        private void Awake()
        {
            RebuildMotorsIfNeeded();
        }

        public void Configure(
            MotorDefinitionAsset newDefinition,
            TrainDriveDefinition newDriveDefinition,
            int newMotorCount)
        {
            definition = newDefinition;
            driveDefinition = newDriveDefinition;
            motorCount = Mathf.Max(0, newMotorCount);
            appliedDefinition = null;
            appliedMotorCount = -1;
            RebuildMotorsIfNeeded();
        }

        public void SetInput(float voltageRmsV, float outputFrequencyHz, float speedMps)
        {
            lineVoltageRmsV = Mathf.Max(0f, voltageRmsV);
            frequencyHz = Mathf.Max(0f, outputFrequencyHz);
            vehicleSpeedMps = speedMps;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            RebuildMotorsIfNeeded();
            ResetAggregateOutput();
            if (!IsAvailable)
            {
                ResetMotors();
                return;
            }

            float wheelAngularSpeedRadPerSecond =
                Mathf.Abs(vehicleSpeedMps) / Mathf.Max(0.01f, driveDefinition.wheelRadiusM);
            float motorRpm = wheelAngularSpeedRadPerSecond * 60f / (2f * Mathf.PI) *
                driveDefinition.gearRatio;

            foreach (MotorContext motor in motors)
            {
                motor.Input.lineVoltageRmsV = lineVoltageRmsV;
                motor.Input.frequencyHz = frequencyHz;
                motor.Input.motorRpm = motorRpm;
                MotorLogic.Calculate(motor);

                AverageMotorTorqueNm += motor.Output.motorTorqueNm;
                TotalMotorCurrentRmsA += motor.Output.motorCurrentRmsA;
                TotalMotorOutputPowerW += motor.Output.motorOutputPowerW;
            }

            if (motors.Count > 0)
            {
                AverageMotorTorqueNm /= motors.Count;
            }

            ActualTractionForceN = AverageMotorTorqueNm * motors.Count *
                driveDefinition.gearRatio * driveDefinition.transmissionEfficiency /
                Mathf.Max(0.01f, driveDefinition.wheelRadiusM);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // Calculateで更新した物理真値を同一ステップの出力とする。
        }

        public void ResetSimulation()
        {
            lineVoltageRmsV = 0f;
            frequencyHz = 0f;
            vehicleSpeedMps = 0f;
            ResetMotors();
            ResetAggregateOutput();
        }

        private void RebuildMotorsIfNeeded()
        {
            int requiredCount = Mathf.Max(0, motorCount);
            if (definition == appliedDefinition && requiredCount == appliedMotorCount)
            {
                return;
            }

            motors.Clear();
            appliedDefinition = definition;
            appliedMotorCount = requiredCount;
            for (int i = 0; i < requiredCount; i++)
            {
                var context = new MotorContext();
                definition?.ApplyTo(context.Settings);
                motors.Add(context);
            }

            ResetAggregateOutput();
        }

        private void ResetMotors()
        {
            foreach (MotorContext motor in motors)
            {
                motor.Input.lineVoltageRmsV = 0f;
                motor.Input.frequencyHz = 0f;
                motor.Input.motorRpm = 0f;
                motor.Output.Reset();
            }
        }

        private void ResetAggregateOutput()
        {
            AverageMotorTorqueNm = 0f;
            ActualTractionForceN = 0f;
            TotalMotorCurrentRmsA = 0f;
            TotalMotorOutputPowerW = 0f;
        }

        private void OnValidate()
        {
            motorCount = Mathf.Max(0, motorCount);
            if (UnityEngine.Application.isPlaying)
            {
                appliedDefinition = null;
                appliedMotorCount = -1;
                RebuildMotorsIfNeeded();
            }
        }
    }
}
