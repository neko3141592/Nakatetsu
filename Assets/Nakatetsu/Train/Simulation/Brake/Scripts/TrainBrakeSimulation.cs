using System.Collections.Generic;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Brake
{
    /// <summary>1両分のブレーキシリンダー物理状態を所有する。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainBrakeSimulation : MonoBehaviour, ISimulationController
    {
        [SerializeField] private BrakeCylinderDefinitionAsset cylinderDefinition;
        [SerializeField, Min(0)] private int cylinderCount = 4;

        private readonly List<BrakeCylinderContext> cylinders = new();
        private BrakeCylinderDefinitionAsset appliedDefinition;
        private int appliedCylinderCount = -1;
        private float targetPressureKPa;

        public BrakeCylinderDefinitionAsset CylinderDefinition => cylinderDefinition;
        public IReadOnlyList<BrakeCylinderContext> Cylinders => cylinders;
        public int CylinderCount => cylinders.Count;
        public int OperationalCylinderCount { get; private set; }
        public float TargetPressureKPa => targetPressureKPa;
        public float ActualBrakeForceN { get; private set; }

        private void Awake()
        {
            RebuildCylindersIfNeeded();
        }

        public void Configure(BrakeCylinderDefinitionAsset definition, int count)
        {
            cylinderDefinition = definition;
            cylinderCount = Mathf.Max(0, count);
            appliedDefinition = null;
            appliedCylinderCount = -1;
            RebuildCylindersIfNeeded();
        }

        public void SetTargetPressureKPa(float value)
        {
            targetPressureKPa = Mathf.Max(0f, value);
        }

        public void SetCylinderHealthy(int cylinderIndex, bool isHealthy)
        {
            RebuildCylindersIfNeeded();
            if (cylinderIndex < 0 || cylinderIndex >= cylinders.Count)
            {
                return;
            }

            cylinders[cylinderIndex].State.isHealthy = isHealthy;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            RebuildCylindersIfNeeded();
            ActualBrakeForceN = 0f;
            OperationalCylinderCount = 0;

            foreach (BrakeCylinderContext cylinder in cylinders)
            {
                cylinder.Input.targetPressureKPa = targetPressureKPa;
                cylinder.Input.deltaTimeSeconds = Mathf.Max(0f, deltaTimeSeconds);
                BrakeCylinderLogic.Calculate(cylinder);
                ActualBrakeForceN += cylinder.Output.actualForceN;
                if (cylinder.State.isHealthy)
                {
                    OperationalCylinderCount++;
                }
            }
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // Calculateで更新した物理真値を同一ステップの出力とする。
        }

        private void RebuildCylindersIfNeeded()
        {
            int requiredCount = Mathf.Max(0, cylinderCount);
            if (cylinderDefinition == appliedDefinition && requiredCount == appliedCylinderCount)
            {
                return;
            }

            cylinders.Clear();
            appliedDefinition = cylinderDefinition;
            appliedCylinderCount = requiredCount;
            for (int i = 0; i < requiredCount; i++)
            {
                var context = new BrakeCylinderContext();
                cylinderDefinition?.ApplyTo(context.Settings);
                cylinders.Add(context);
            }

            OperationalCylinderCount = requiredCount;
            ActualBrakeForceN = 0f;
        }

        private void OnValidate()
        {
            cylinderCount = Mathf.Max(0, cylinderCount);
            if (Application.isPlaying)
            {
                appliedDefinition = null;
                appliedCylinderCount = -1;
                RebuildCylindersIfNeeded();
            }
        }
    }
}
