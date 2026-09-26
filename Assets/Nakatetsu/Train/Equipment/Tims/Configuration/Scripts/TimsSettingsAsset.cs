using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Nakatetsu.Train.Equipment.Tims.Configuration
{
    
    [CreateAssetMenu(fileName = "TimsControlConfig", menuName = "Nakatetsu/Train/Settings")]
    public class TimsSettingsAsset : ScriptableObject
    {
        [Header("Notch")]
        [FormerlySerializedAs("powerStepCount")]
        [Min(1)] public int powerNotchCount = 4;

        [Header("Traction")]
        [Tooltip("力行カーブの横軸1に対応する最高運転速度（km/h）。")]
        [Min(1f)] public float maximumOperatingSpeedKmh = 120f;
        public AnimationCurve[] powerCurves;
        [Min(0f)] public float launchAccelerationKmhPerSec = 3f;

        [Header("Brake")]
        public List<float> brakeTargetDecelerationsKmhPerSec = new();
        [FormerlySerializedAs("brakeStepCount")]
        [Min(1)] public int brakeNotchCount = 7;
        [FormerlySerializedAs("masterControllerBrakePosition")]
        [Min(2)] public int masterControllerEmergencyBrakeNotchPosition = 8;
        [Min(1)] public int brakeSubstepCount = 4;

        [Min(0f)] public float minimumServiceBrakePressureKPa = 40f;
        [Min(1f)] public float minimumServiceBrakePressureLoadScaleMax = 3f;

        private void OnValidate()
        {
            powerNotchCount = Mathf.Max(1, powerNotchCount);
            maximumOperatingSpeedKmh = Mathf.Max(1f, maximumOperatingSpeedKmh);
            launchAccelerationKmhPerSec = Mathf.Max(0f, launchAccelerationKmhPerSec);
            brakeNotchCount = Mathf.Max(1, brakeNotchCount);
            masterControllerEmergencyBrakeNotchPosition = Mathf.Max(
                brakeNotchCount + 1,
                masterControllerEmergencyBrakeNotchPosition);
            brakeSubstepCount = Mathf.Max(1, brakeSubstepCount);
            minimumServiceBrakePressureKPa = Mathf.Max(0f, minimumServiceBrakePressureKPa);
            minimumServiceBrakePressureLoadScaleMax = Mathf.Max(1f, minimumServiceBrakePressureLoadScaleMax);
        }
    }
}
