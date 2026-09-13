using UnityEngine;

namespace Nakatetsu.Train.Equipment.Brake.Cylinder
{
    [DisallowMultipleComponent]
    public sealed class BrakeCylinder : MonoBehaviour
    {
        [SerializeField] private BrakeCylinderDefinitionAsset definition;

        private readonly BrakeCylinderContext context = new();

        public BrakeCylinderSettings Settings => context.Settings;
        public BrakeCylinderState State => context.State;
        public float CurrentPressureKPa => context.State.currentPressureKPa;
        public float ActualForceN => context.Output.actualForceN;
        public bool IsHealthy => context.State.isHealthy;

        private void Awake()
        {
            ApplyDefinition();
        }

        public void Configure(BrakeCylinderDefinitionAsset newDefinition)
        {
            definition = newDefinition;
            ApplyDefinition();
        }

        public void SetHealthy(bool isHealthy)
        {
            context.State.isHealthy = isHealthy;
        }

        public void Step(float targetPressureKPa, float deltaTimeSeconds)
        {
            context.Input.targetPressureKPa = targetPressureKPa;
            context.Input.deltaTimeSeconds = deltaTimeSeconds;
            BrakeCylinderLogic.Calculate(context);
        }

        private void ApplyDefinition()
        {
            if (definition != null)
            {
                definition.ApplyTo(context.Settings);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyDefinition();
            }
        }
    }
}
