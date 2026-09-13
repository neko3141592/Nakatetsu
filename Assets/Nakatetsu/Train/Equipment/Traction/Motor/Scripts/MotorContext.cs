using System;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Motor
{
    [Serializable]
    public sealed class MotorSettings
    {
        [Header("Rated Values")]
        [Min(0f)] public float ratedPowerW = 140000f;
        [Min(0f)] public float ratedLineVoltageV = 1050f;
        [Min(0f)] public float ratedCurrentA = 108f;
        [Min(0.01f)] public float ratedFrequencyHz = 80f;
        [Min(0.01f)] public float ratedRpm = 2380f;

        [Header("Motor Geometry")]
        [Min(2)] public int poleCount = 4;

        [Header("Characteristics")]
        [Range(0f, 1f)] public float efficiency = 0.945f;
        [Range(0f, 1f)] public float powerFactor = 0.755f;
        [Range(0f, 0.2f)] public float ratedSlipRatio = 0.007f;

        [Header("Equivalent Circuit at Rated Frequency")]
        [Min(0f)] public float statorResistanceOhm = 0.12f;
        [Min(0f)] public float statorReactanceOhm = 0.60f;
        [Min(0f)] public float magnetizingReactanceOhm = 10f;
        [Min(0f)] public float rotorResistanceOhm = 0.045f;
        [Min(0f)] public float rotorReactanceOhm = 0.80f;

        public void CopyFrom(MotorSettings source)
        {
            if (source == null) return;

            ratedPowerW = source.ratedPowerW;
            ratedLineVoltageV = source.ratedLineVoltageV;
            ratedCurrentA = source.ratedCurrentA;
            ratedFrequencyHz = source.ratedFrequencyHz;
            ratedRpm = source.ratedRpm;
            poleCount = source.poleCount;
            efficiency = source.efficiency;
            powerFactor = source.powerFactor;
            ratedSlipRatio = source.ratedSlipRatio;
            statorResistanceOhm = source.statorResistanceOhm;
            statorReactanceOhm = source.statorReactanceOhm;
            magnetizingReactanceOhm = source.magnetizingReactanceOhm;
            rotorResistanceOhm = source.rotorResistanceOhm;
            rotorReactanceOhm = source.rotorReactanceOhm;
        }
    }

    public sealed class MotorInput
    {
        public float lineVoltageRmsV;
        public float frequencyHz;
        public float motorRpm;
    }

    public sealed class MotorOutput
    {
        public float synchronousRpm;
        public float slipRatio;
        public float ratedTorqueNm;
        public float motorTorqueNm;
        public float motorCurrentRmsA;
        public float rotorCurrentRmsA;
        public float motorOutputPowerW;
        public float inputActivePowerW;
        public float apparentPowerVA;

        public void Reset()
        {
            synchronousRpm = 0f;
            slipRatio = 0f;
            ratedTorqueNm = 0f;
            motorTorqueNm = 0f;
            motorCurrentRmsA = 0f;
            rotorCurrentRmsA = 0f;
            motorOutputPowerW = 0f;
            inputActivePowerW = 0f;
            apparentPowerVA = 0f;
        }
    }

    public sealed class MotorContext
    {
        public MotorSettings Settings { get; } = new();
        public MotorInput Input { get; } = new();
        public MotorOutput Output { get; } = new();
    }
}
