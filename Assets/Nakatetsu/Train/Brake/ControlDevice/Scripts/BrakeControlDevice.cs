using System.Collections.Generic;
using UnityEngine;
using Nakatetsu.Train.Brake.Cylinder;

namespace Nakatetsu.Train.Brake.ControlDevice
{
    [DisallowMultipleComponent]
    public sealed class BrakeControlDevice : MonoBehaviour
    {
        [SerializeField] private List<BrakeCylinder> brakeCylinders = new();

        private readonly BrakeControlDeviceContext context = new();
        private float targetBrakeForceN;

        public IReadOnlyList<BrakeCylinder> BrakeCylinders => brakeCylinders;
        public float TargetBrakeForceN => targetBrakeForceN;
        public float TargetPressureKPa => context.Output.targetPressureKPa;

        public float ActualBrakeForceN
        {
            get
            {
                float total = 0f;
                foreach (BrakeCylinder cylinder in brakeCylinders)
                {
                    if (cylinder != null) total += cylinder.ActualForceN;
                }
                return total;
            }
        }

        private void Awake()
        {
            RefreshBrakeCylinders();
        }

        public void SetTargetBrakeForceN(float value)
        {
            targetBrakeForceN = Mathf.Max(0f, value);
        }

        public void Step(float value, float deltaTimeSeconds)
        {
            SetTargetBrakeForceN(value);
            PopulateInput();
            BrakeControlDeviceLogic.Calculate(context);

            foreach (BrakeCylinder cylinder in brakeCylinders)
            {
                if (cylinder != null)
                {
                    cylinder.Step(context.Output.targetPressureKPa, deltaTimeSeconds);
                }
            }
        }

        public void RefreshBrakeCylinders()
        {
            brakeCylinders.Clear();
            brakeCylinders.AddRange(GetComponentsInChildren<BrakeCylinder>(true));
        }

        private void PopulateInput()
        {
            context.Input.targetBrakeForceN = targetBrakeForceN;
            int inputIndex = 0;

            foreach (BrakeCylinder cylinder in brakeCylinders)
            {
                if (cylinder == null) continue;

                BrakeCylinderSettings settings = cylinder.Settings;
                if (inputIndex >= context.Input.cylinders.Count)
                {
                    context.Input.cylinders.Add(new BrakeControlCylinderInput());
                }

                BrakeControlCylinderInput input = context.Input.cylinders[inputIndex];
                input.isHealthy = cylinder.IsHealthy;
                input.maximumPressureKPa = settings.maximumPressureKPa;
                input.pistonAreaM2 = settings.pistonAreaM2;
                input.mechanicalEfficiency = settings.mechanicalEfficiency;
                inputIndex++;
            }

            if (context.Input.cylinders.Count > inputIndex)
            {
                context.Input.cylinders.RemoveRange(
                    inputIndex,
                    context.Input.cylinders.Count - inputIndex);
            }
        }

        private void OnValidate()
        {
            RefreshBrakeCylinders();
        }
    }
}
