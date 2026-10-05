using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainAtcController))]
    public sealed class TrainAtcTimsDisplayAdapter : MonoBehaviour, ITimsMasterBusSource
    {
        public static readonly TimsTagKey IsPowerOnKey = new("ATC", "IsPowerOn");
        public static readonly TimsTagKey IsEnabledKey = new("ATC", "IsEnabled");
        public static readonly TimsTagKey IsHealthyKey = new("ATC", "IsHealthy");
        public static readonly TimsTagKey HasFaultKey = new("ATC", "HasFault");
        public static readonly TimsTagKey IsBrakeReleasedKey = new("ATC", "IsBrakeReleased");
        public static readonly TimsTagKey IsNormalBrakeRequiredKey = new("ATC", "IsNormalBrakeRequired");
        public static readonly TimsTagKey IsEmergencyBrakeRequiredKey = new("ATC", "IsEmergencyBrakeRequired");
        public static readonly TimsTagKey IsRollingPreventingKey = new("ATC", "IsRollingPreventing");
        public static readonly TimsTagKey BrakeStepKey = new("ATC", "BrakeStep");
        public static readonly TimsTagKey HasValidPatternKey = new("ATC", "HasValidPattern");
        public static readonly TimsTagKey IsSpeedIndicatedKey = new("ATC", "IsSpeedIndicated");
        public static readonly TimsTagKey IsOrpActiveKey = new("ATC", "IsOrpActive");
        public static readonly TimsTagKey IsPatternApproachingKey = new("ATC", "IsPatternApproaching");
        public static readonly TimsTagKey SignalKey = new("ATC", "Signal");
        public static readonly TimsTagKey PatternAllowSpeedMpsKey = new("ATC", "PatternAllowSpeedMps");
        public static readonly TimsTagKey PatternAllowSpeedKmhKey = new("ATC", "PatternAllowSpeedKmh");
        public static readonly TimsTagKey EmergencyPatternAllowSpeedKmhKey = new("ATC", "EmergencyPatternAllowSpeedKmh");
        public static readonly TimsTagKey NormalSpeedPatternMpsKey = new("ATC", "NormalSpeedPatternMps");
        public static readonly TimsTagKey EmergencySpeedPatternMpsKey = new("ATC", "EmergencySpeedPatternMps");
        public static readonly TimsTagKey PatternAtcEdgeIdsKey = new("ATC", "PatternAtcEdgeIds");
        public static readonly TimsTagKey SamplingIntervalMKey = new("ATC", "SamplingIntervalM");
        public static readonly TimsTagKey PathLengthMKey = new("ATC", "PathLengthM");
        public static readonly TimsTagKey DistanceOnPathMKey = new("ATC", "DistanceOnPathM");

        private TrainRoot trainRoot;
        private TrainAtcController atc;
        private TimsBusState publishedBus;

        public void WriteTimsBus(TimsBusState masterBus)
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
            if (atc == null)
            {
                atc = GetComponent<TrainAtcController>();
            }
            if (publishedBus != masterBus)
            {
                if (publishedBus != null)
                {
                    ClearDisplay(publishedBus);
                }
                publishedBus = masterBus;
            }
            if (masterBus == null)
            {
                return;
            }

            var bus = masterBus;
            if (!isActiveAndEnabled || trainRoot == null || !trainRoot.isActiveAndEnabled ||
                atc == null || !atc.isActiveAndEnabled)
            {
                ClearDisplay(bus);
                return;
            }

            // TIMSの収集時点で、ATCが最後に計算した表示情報を読み取る。
            var context = atc.Context;
            var output = context.Output;
            bool hasValidPattern = context.State.pattern.isValid &&
                output.state.isAtcEnabled && output.state.isAtcHealthy;

            bus.SetBool(IsPowerOnKey, output.state.isAtcPowerOn);
            bus.SetBool(IsEnabledKey, output.state.isAtcEnabled);
            bus.SetBool(IsHealthyKey, output.state.isAtcHealthy);
            bus.SetBool(HasFaultKey, output.state.isAtcPowerOn && !output.state.isAtcHealthy);
            bus.SetBool(IsBrakeReleasedKey, output.state.isBrakeReleased);
            bus.SetBool(IsNormalBrakeRequiredKey, output.brake.isNormalBrakeRequired);
            bus.SetBool(IsEmergencyBrakeRequiredKey, output.brake.isEmergencyBrakeRequired);
            bus.SetBool(IsRollingPreventingKey, output.brake.isRollingPreventing);
            bus.SetInt(BrakeStepKey, output.brake.brakeStep);
            bus.SetBool(HasValidPatternKey, hasValidPattern);
            bus.SetBool(IsSpeedIndicatedKey, output.pattern.isSpeedIndicated);
            bus.SetBool(IsOrpActiveKey, output.pattern.isOrpOperating);
            bus.SetBool(IsPatternApproachingKey, output.pattern.isPatternApproaching);
            bus.SetInt(SignalKey, (int)output.pattern.signal);

            // 速度は現示可否にかかわらず転送し、UIがIsSpeedIndicatedで点灯を決める。
            bus.SetFloat(PatternAllowSpeedMpsKey, output.pattern.allowSpeedMps);
            bus.SetFloat(PatternAllowSpeedKmhKey, output.pattern.indicatedSpeedKmh);

            // パターン全体の診断情報も既存のキーで公開する。配列はBus側でもコピーされる。
            var pattern = context.State.pattern;
            bus.SetFloat(EmergencyPatternAllowSpeedKmhKey, pattern.emergencyPattern.currentAllowSpeedMps * 3.6f);
            bus.SetFloatArray(NormalSpeedPatternMpsKey, GetPatternSpeeds(pattern.normalPattern.samples));
            bus.SetFloatArray(EmergencySpeedPatternMpsKey, GetPatternSpeeds(pattern.emergencyPattern.samples));
            bus.SetStringArray(PatternAtcEdgeIdsKey, pattern.atcEdgePath.ToArray());
            bus.SetFloat(SamplingIntervalMKey, pattern.samplingIntervalM);
            bus.SetFloat(PathLengthMKey, pattern.pathLengthM);
            bus.SetFloat(DistanceOnPathMKey, pattern.distanceOnPathM);
        }

        private static float[] GetPatternSpeeds(List<TrainAtcPatternSample> samples)
        {
            var speeds = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                speeds[i] = samples[i].allowSpeedMps;
            }
            return speeds;
        }

        private static void ClearPatternValues(TimsBusState bus)
        {
            bus.SetFloat(PatternAllowSpeedMpsKey, 0f);
            bus.SetFloat(PatternAllowSpeedKmhKey, 0f);
            bus.SetFloat(EmergencyPatternAllowSpeedKmhKey, 0f);
            bus.SetFloatArray(NormalSpeedPatternMpsKey, Array.Empty<float>());
            bus.SetFloatArray(EmergencySpeedPatternMpsKey, Array.Empty<float>());
            bus.SetStringArray(PatternAtcEdgeIdsKey, Array.Empty<string>());
            bus.SetFloat(SamplingIntervalMKey, 0f);
            bus.SetFloat(PathLengthMKey, 0f);
            bus.SetFloat(DistanceOnPathMKey, 0f);
        }

        private static void ClearDisplay(TimsBusState bus)
        {
            bus.SetBool(IsPowerOnKey, false);
            bus.SetBool(IsEnabledKey, false);
            bus.SetBool(IsHealthyKey, false);
            bus.SetBool(HasFaultKey, false);
            bus.SetBool(IsBrakeReleasedKey, false);
            bus.SetBool(IsNormalBrakeRequiredKey, false);
            bus.SetBool(IsEmergencyBrakeRequiredKey, false);
            bus.SetBool(IsRollingPreventingKey, false);
            bus.SetInt(BrakeStepKey, 0);
            bus.SetBool(HasValidPatternKey, false);
            bus.SetBool(IsSpeedIndicatedKey, false);
            bus.SetBool(IsOrpActiveKey, false);
            bus.SetBool(IsPatternApproachingKey, false);
            bus.SetInt(SignalKey, (int)TrainAtcSignal.None);
            ClearPatternValues(bus);
        }

        private void OnDisable()
        {
            if (publishedBus != null)
            {
                ClearDisplay(publishedBus);
            }
        }
    }
}
