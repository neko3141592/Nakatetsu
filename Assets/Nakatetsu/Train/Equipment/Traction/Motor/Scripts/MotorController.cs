using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Motor
{
    [DisallowMultipleComponent]
    public sealed class MotorController : MonoBehaviour
    {
        [SerializeField] private MotorDefinitionAsset definition;

        private readonly MotorContext context = new();
        private MotorDefinitionAsset appliedDefinition;

        public MotorDefinitionAsset Definition => definition;
        public MotorSettings Settings => context.Settings;
        public MotorOutput Output => context.Output;
        public float MotorTorqueNm => context.Output.motorTorqueNm;
        public float RatedPowerW => context.Settings.ratedPowerW;

        private void Awake()
        {
            ApplyDefinition();
        }

        public void Configure(MotorDefinitionAsset newDefinition)
        {
            definition = newDefinition;
            appliedDefinition = null;
            ApplyDefinition();
        }

        public void Step(float lineVoltageRmsV, float frequencyHz, float motorRpm)
        {
            ApplyDefinition();
            if (appliedDefinition == null)
            {
                ResetMotor();
                return;
            }

            context.Input.lineVoltageRmsV = lineVoltageRmsV;
            context.Input.frequencyHz = frequencyHz;
            context.Input.motorRpm = motorRpm;
            MotorLogic.Calculate(context);
        }

        public void ResetMotor()
        {
            context.Input.lineVoltageRmsV = 0f;
            context.Input.frequencyHz = 0f;
            context.Input.motorRpm = 0f;
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
            if (Application.isPlaying)
            {
                appliedDefinition = null;
                ApplyDefinition();
            }
        }
    }
}
