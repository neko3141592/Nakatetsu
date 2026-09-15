using Nakatetsu.Train.Equipment.Shared;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.SpeedMeasurement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class SpeedSensor : MonoBehaviour, IEquipmentController
    {
        private readonly SpeedSensorContext context = new();
        private bool hasPhysicalSpeed;
        private float signedPhysicalSpeedMps;

        public void SetPhysicalSpeedMps(float signedSpeedMps)
        {
            signedPhysicalSpeedMps = signedSpeedMps;
            hasPhysicalSpeed = true;
        }

        public void ClearPhysicalSpeed()
        {
            hasPhysicalSpeed = false;
            context.Output.hasMeasurement = false;
        }

        public bool TryGetMeasuredSpeedMps(out float speedMps)
        {
            speedMps = 0f;
            if (!isActiveAndEnabled || !context.Output.hasMeasurement)
            {
                return false;
            }

            speedMps = context.Output.measuredSpeedMps;
            return true;
        }

        public void CollectInput()
        {
            context.Input.hasPhysicalSpeed = isActiveAndEnabled && hasPhysicalSpeed;
            context.Input.signedPhysicalSpeedMps = signedPhysicalSpeedMps;
            // 物理入力は測定ステップごとに必要。未供給時に前回値を再測定しない。
            hasPhysicalSpeed = false;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            SpeedSensorLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 測定値はTIMS側のBusSourceが読み取る。
        }

        private void OnDisable()
        {
            ClearPhysicalSpeed();
        }
    }
}
