using UnityEngine;
using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Tims.Configuration
{
    
    [CreateAssetMenu(fileName = "TimsControlConfig", menuName = "Nakatetsu/Train/Settings")]
    public class TimsSettingsAsset : ScriptableObject
    {
        [Header("Notch")]
        [Min(1)] public int powerStepCount = 5;

        [Header("Traction")]
        public AnimationCurve[] powerCurves;
        [Min(0f)] public float launchAccelerationKmhPerSec = 3f;

        [Header("Brake")]
        public List<float> brakeTargetDecelerationsKmhPerSec = new();
        [Min(1)] public int brakeStepCount = 7;
        [Min(1)] public int brakeSubstepCount = 4;

        [Min(0f)] public float minimumServiceBrakePressureKPa = 40f;
        [Min(1f)] public float minimumServiceBrakePressureLoadScaleMax = 3f;

        private void OnValidate()
        {
            powerStepCount = Mathf.Max(1, powerStepCount);
            launchAccelerationKmhPerSec = Mathf.Max(0f, launchAccelerationKmhPerSec);
            brakeStepCount = Mathf.Max(1, brakeStepCount);
            brakeSubstepCount = Mathf.Max(1, brakeSubstepCount);
            minimumServiceBrakePressureKPa = Mathf.Max(0f, minimumServiceBrakePressureKPa);
            minimumServiceBrakePressureLoadScaleMax = Mathf.Max(1f, minimumServiceBrakePressureLoadScaleMax);
        }
    }
}