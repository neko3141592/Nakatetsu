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
        public static readonly TimsTagKey IsNormalBrakeRequiredKey = new("ATC", "IsNormalBrakeRequired");
        public static readonly TimsTagKey HasValidPatternKey = new("ATC", "HasValidPattern");
        public static readonly TimsTagKey IsOrpActiveKey = new("ATC", "IsOrpActive");
        public static readonly TimsTagKey IsPatternApproachingKey = new("ATC", "IsPatternApproaching");
        public static readonly TimsTagKey SignalKey = new("ATC", "Signal");
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
            if (trainRoot == null) trainRoot = GetComponentInParent<TrainRoot>(true);
            if (atc == null) atc = GetComponent<TrainAtcController>();
            if (publishedBus != masterBus)
            {
                if (publishedBus != null) ClearDisplay(publishedBus);
                publishedBus = masterBus;
            }
            if (masterBus == null) return;

            var bus = masterBus;
            if (!isActiveAndEnabled || trainRoot == null || !trainRoot.isActiveAndEnabled ||
                atc == null || !atc.isActiveAndEnabled)
            {
                ClearDisplay(bus);
                return;
            }

            // TIMSの収集時点で、ATCが最後に計算した表示情報を読み取る。
            var context = atc.Context;
            var state = context.State;
            var output = context.Output;
            bus.SetBool(IsPowerOnKey, state.isAtcPowerOn);
            bus.SetBool(IsEnabledKey, state.isAtcEnabled);
            bus.SetBool(IsHealthyKey, state.isHealthy);
            bus.SetBool(HasFaultKey, state.isAtcPowerOn && !state.isHealthy);
            bus.SetBool(IsNormalBrakeRequiredKey, state.brake.isNormalRequired);
            bus.SetBool(HasValidPatternKey, output.hasValidPattern);
            bus.SetBool(IsOrpActiveKey, output.hasValidPattern && output.isOrpActive);
            bus.SetBool(IsPatternApproachingKey, output.hasValidPattern && output.isPatternApproaching);
            bus.SetInt(SignalKey, (int)output.signal);
            if (!output.hasValidPattern)
            {
                ClearPatternTags(bus);
                return;
            }

            // 編成共通のATCからMasterBusへ公開する。配列はBus側でもコピーされる。
            var pattern = state.brakePattern;
            float atcIndicatedSpeedKmh = output.normalAllowSpeedMps * 3.6f;
            // ORPの表示中は、ATCの現示速度を0km/hにする。
            if (output.isOrpActive)
            {
                atcIndicatedSpeedKmh = 0f;
            }
            bus.SetFloat(PatternAllowSpeedKmhKey, atcIndicatedSpeedKmh);
            bus.SetFloat(EmergencyPatternAllowSpeedKmhKey, output.emergencyAllowSpeedMps * 3.6f);
            bus.SetFloatArray(NormalSpeedPatternMpsKey, GetPatternSpeeds(pattern.normalPattern));
            bus.SetFloatArray(EmergencySpeedPatternMpsKey, GetPatternSpeeds(pattern.emergencyPattern));
            bus.SetStringArray(PatternAtcEdgeIdsKey, pattern.pathAtcEdges.ToArray());
            bus.SetFloat(SamplingIntervalMKey, pattern.samplingIntervalM);
            bus.SetFloat(PathLengthMKey, pattern.pathLengthM);
            bus.SetFloat(DistanceOnPathMKey, output.distanceOnPathM);
        }

        private static float[] GetPatternSpeeds(List<TrainAtcBrakePatternSample> samples)
        {
            var speeds = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                speeds[i] = samples[i].speedLimitMps;
            }
            return speeds;
        }

        private static void ClearPatternTags(TimsBusState bus)
        {
            bus.Remove(PatternAllowSpeedKmhKey);
            bus.Remove(EmergencyPatternAllowSpeedKmhKey);
            bus.Remove(NormalSpeedPatternMpsKey);
            bus.Remove(EmergencySpeedPatternMpsKey);
            bus.Remove(PatternAtcEdgeIdsKey);
            bus.Remove(SamplingIntervalMKey);
            bus.Remove(PathLengthMKey);
            bus.Remove(DistanceOnPathMKey);
        }

        private static void ClearDisplay(TimsBusState bus)
        {
            bus.SetBool(IsPowerOnKey, false);
            bus.SetBool(IsEnabledKey, false);
            bus.SetBool(IsHealthyKey, false);
            bus.SetBool(HasFaultKey, false);
            bus.SetBool(IsNormalBrakeRequiredKey, false);
            bus.SetBool(HasValidPatternKey, false);
            bus.SetBool(IsOrpActiveKey, false);
            bus.SetBool(IsPatternApproachingKey, false);
            bus.SetInt(SignalKey, (int)TrainAtcSignal.None);
            ClearPatternTags(bus);
        }

        private void OnDisable()
        {
            if (publishedBus != null) ClearDisplay(publishedBus);
        }
    }
}
