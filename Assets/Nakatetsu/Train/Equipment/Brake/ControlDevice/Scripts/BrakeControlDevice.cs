using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Simulation.Brake;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Brake.ControlDevice
{
    [DisallowMultipleComponent]
    public sealed class BrakeControlDevice : MonoBehaviour, IEquipmentController
    {
        [SerializeField] private BrakeCylinderDefinitionAsset cylinderDefinition;
        [SerializeField, Min(0)] private int cylinderCount = 4;

        private readonly BrakeControlDeviceContext context = new();
        private float targetBrakeForceN;
        private float measuredActualBrakeForceN;
        private int measuredOperationalCylinderCount = -1;
        private IBrakeCommandSource brakeCommandSource;

        public int CylinderCount => Mathf.Max(0, cylinderCount);
        public float TargetBrakeForceN => targetBrakeForceN;
        public float TargetPressureKPa => context.Output.targetPressureKPa;
        public float ActualBrakeForceN => measuredActualBrakeForceN;
        // MVPの測定入力。物理モデルはSimulation側が所有する。
        public float MeasuredMassKg { get; private set; }
        public float ForcePerKPa => cylinderDefinition == null ? 0f :
            cylinderDefinition.Settings.pistonAreaM2 * 1000f *
            cylinderDefinition.Settings.mechanicalEfficiency *
            (measuredOperationalCylinderCount >= 0 ? measuredOperationalCylinderCount : CylinderCount);
        public float MaximumPressureKPa => cylinderDefinition != null
            ? cylinderDefinition.Settings.maximumPressureKPa : 0f;
        public float MaximumBrakeForceN => ForcePerKPa * MaximumPressureKPa;
        public float MeasuredPressureKPa => ForcePerKPa > 0f ? ActualBrakeForceN / ForcePerKPa : 0f;

        public void SetMassMeasurement(float massKg)
        {
            MeasuredMassKg = massKg;
        }

        private void Awake()
        {
            ResolveBrakeCommandSource();
        }

        public void Configure(BrakeCylinderDefinitionAsset definition, int count)
        {
            cylinderDefinition = definition;
            cylinderCount = Mathf.Max(0, count);
        }

        public void SetSimulationMeasurement(float actualBrakeForceN, int operationalCylinderCount)
        {
            measuredActualBrakeForceN = Mathf.Max(0f, actualBrakeForceN);
            measuredOperationalCylinderCount = Mathf.Clamp(
                operationalCylinderCount,
                0,
                CylinderCount);
        }

        public void SetTargetBrakeForceN(float value)
        {
            targetBrakeForceN = Mathf.Max(0f, value);
        }

        public void CollectInput()
        {
            ReadTargetBrakeForce();
            PopulateInput();
        }

        public void Calculate(float deltaTimeSeconds)
        {
            BrakeControlDeviceLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 物理モデルはTrainBrakeSimulationが進める。Equipmentは目標圧力だけを公開する。
        }

        private void ReadTargetBrakeForce()
        {
            if (brakeCommandSource == null)
            {
                ResolveBrakeCommandSource();
            }

            if (brakeCommandSource != null)
            {
                // 通信接続済みの指令欠損は最大空気制動。手動Setterのみの利用は維持する。
                SetTargetBrakeForceN(brakeCommandSource.TryGetTargetBrakeForceN(out float targetForceN)
                    ? targetForceN : MaximumBrakeForceN);
            }
        }

        private void ResolveBrakeCommandSource()
        {
            brakeCommandSource = null;
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is IBrakeCommandSource commandSource)
                {
                    brakeCommandSource = commandSource;
                    return;
                }
            }
        }

        private void PopulateInput()
        {
            context.Input.targetBrakeForceN = targetBrakeForceN;
            context.Input.cylinders.Clear();
            if (cylinderDefinition == null)
            {
                return;
            }

            BrakeCylinderSettings settings = cylinderDefinition.Settings;
            int operationalCount = measuredOperationalCylinderCount >= 0
                ? measuredOperationalCylinderCount
                : CylinderCount;
            for (int i = 0; i < CylinderCount; i++)
            {
                context.Input.cylinders.Add(new BrakeControlCylinderInput
                {
                    isHealthy = i < operationalCount,
                    maximumPressureKPa = settings.maximumPressureKPa,
                    pistonAreaM2 = settings.pistonAreaM2,
                    mechanicalEfficiency = settings.mechanicalEfficiency
                });
            }
        }

        private void OnValidate()
        {
            cylinderCount = Mathf.Max(0, cylinderCount);
        }
    }
}
