using System;
using UnityEngine;
using Nakatetsu.Train.Traction.Electrical;

namespace Nakatetsu.Train.Traction.Motor
{
    public static class MotorLogic
    {
        public static void Calculate(MotorContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            MotorSettings settings = context.Settings;
            MotorInput input = context.Input;
            MotorOutput output = context.Output;

            output.ratedTorqueNm = GetTorqueFromPowerAndRpm(
                settings.ratedPowerW,
                settings.ratedRpm);

            if (input.frequencyHz < 0.01f || input.lineVoltageRmsV < 1f)
            {
                float ratedTorqueNm = output.ratedTorqueNm;
                output.Reset();
                output.ratedTorqueNm = ratedTorqueNm;
                return;
            }

            output.synchronousRpm = GetSynchronousRpm(input.frequencyHz, settings.poleCount);
            output.slipRatio = GetSlipRatio(output.synchronousRpm, input.motorRpm);
            if (Mathf.Abs(output.slipRatio) < 0.0001f)
            {
                ClearElectricalOutput(output);
                return;
            }

            float frequencyRatio = input.frequencyHz / Mathf.Max(0.01f, settings.ratedFrequencyHz);
            float rotorResistanceBySlipOhm = settings.rotorResistanceOhm / output.slipRatio;
            ComplexValue statorImpedance = settings.statorResistanceOhm +
                ComplexValue.J * (settings.statorReactanceOhm * frequencyRatio);
            ComplexValue magnetizingImpedance =
                ComplexValue.J * (settings.magnetizingReactanceOhm * frequencyRatio);
            ComplexValue rotorImpedance = rotorResistanceBySlipOhm +
                ComplexValue.J * (settings.rotorReactanceOhm * frequencyRatio);
            ComplexValue totalImpedance = statorImpedance +
                CalculateParallelImpedance(magnetizingImpedance, rotorImpedance);

            float phaseVoltageRmsV = input.lineVoltageRmsV / Mathf.Sqrt(3f);
            ComplexValue statorCurrent = new ComplexValue(phaseVoltageRmsV, 0f) / totalImpedance;
            ComplexValue rotorCurrent = statorCurrent * magnetizingImpedance /
                (rotorImpedance + magnetizingImpedance);

            output.motorCurrentRmsA = statorCurrent.Magnitude;
            output.rotorCurrentRmsA = rotorCurrent.Magnitude;
            output.apparentPowerVA = Mathf.Sqrt(3f) * input.lineVoltageRmsV * output.motorCurrentRmsA;

            float airGapPowerW = 3f * output.rotorCurrentRmsA * output.rotorCurrentRmsA *
                rotorResistanceBySlipOhm;
            float synchronousAngularSpeedRadS = GetAngularSpeedRadS(output.synchronousRpm);
            output.motorTorqueNm = airGapPowerW /
                Mathf.Max(0.1f, Mathf.Abs(synchronousAngularSpeedRadS));
            output.motorOutputPowerW = output.motorTorqueNm * GetAngularSpeedRadS(input.motorRpm);
            output.inputActivePowerW = output.motorOutputPowerW /
                Mathf.Max(0.01f, settings.efficiency);
        }

        public static float GetSynchronousRpm(float frequencyHz, int poleCount) =>
            poleCount > 0 ? 120f * frequencyHz / poleCount : 0f;

        public static float GetFrequencyFromSynchronousRpm(float synchronousRpm, int poleCount) =>
            poleCount > 0 ? synchronousRpm * poleCount / 120f : 0f;

        public static float GetSlipRatio(float synchronousRpm, float motorRpm) =>
            Mathf.Abs(synchronousRpm) > 1f
                ? (synchronousRpm - motorRpm) / synchronousRpm
                : 0f;

        public static float GetAngularSpeedRadS(float rpm) =>
            rpm * 2f * Mathf.PI / 60f;

        public static float GetTorqueFromPowerAndRpm(float powerW, float rpm)
        {
            float angularSpeedRadS = GetAngularSpeedRadS(rpm);
            return Mathf.Abs(angularSpeedRadS) > 0.01f
                ? powerW / angularSpeedRadS
                : 0f;
        }

        private static ComplexValue CalculateParallelImpedance(
            ComplexValue first,
            ComplexValue second)
        {
            ComplexValue admittance = 1f / first + 1f / second;
            return 1f / admittance;
        }

        private static void ClearElectricalOutput(MotorOutput output)
        {
            output.motorTorqueNm = 0f;
            output.motorCurrentRmsA = 0f;
            output.rotorCurrentRmsA = 0f;
            output.motorOutputPowerW = 0f;
            output.inputActivePowerW = 0f;
            output.apparentPowerVA = 0f;
        }
    }
}
