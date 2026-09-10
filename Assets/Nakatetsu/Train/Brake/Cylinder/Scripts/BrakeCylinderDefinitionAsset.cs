using System;
using UnityEngine;

namespace Nakatetsu.Train.Brake.Cylinder
{
    [CreateAssetMenu(
        fileName = "BrakeCylinderDefinition",
        menuName = "Nakatetsu/Train/Brake/Brake Cylinder Definition")]
    public sealed class BrakeCylinderDefinitionAsset : ScriptableObject
    {
        [Min(0f)] public float maximumPressureKPa = 500f;
        [Min(0f)] public float applyRateKPaPerSecond = 250f;
        [Min(0f)] public float releaseRateKPaPerSecond = 350f;
        [Min(0f)] public float pistonAreaM2 = 0.01f;
        [Range(0f, 1f)] public float mechanicalEfficiency = 0.9f;

        public void ApplyTo(BrakeCylinderSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            settings.maximumPressureKPa = Mathf.Max(0f, maximumPressureKPa);
            settings.applyRateKPaPerSecond = Mathf.Max(0f, applyRateKPaPerSecond);
            settings.releaseRateKPaPerSecond = Mathf.Max(0f, releaseRateKPaPerSecond);
            settings.pistonAreaM2 = Mathf.Max(0f, pistonAreaM2);
            settings.mechanicalEfficiency = Mathf.Clamp01(mechanicalEfficiency);
        }

        private void OnValidate()
        {
            maximumPressureKPa = Mathf.Max(0f, maximumPressureKPa);
            applyRateKPaPerSecond = Mathf.Max(0f, applyRateKPaPerSecond);
            releaseRateKPaPerSecond = Mathf.Max(0f, releaseRateKPaPerSecond);
            pistonAreaM2 = Mathf.Max(0f, pistonAreaM2);
            mechanicalEfficiency = Mathf.Clamp01(mechanicalEfficiency);
        }
    }
}
