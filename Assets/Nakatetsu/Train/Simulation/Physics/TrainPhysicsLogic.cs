using UnityEngine;

namespace Nakatetsu.Train.Simulation.Physics
{
    public static class TrainPhysicsLogic
    {
        private const float StopThresholdMps = 0.01f;
        private const float GravityMps2 = 9.80665f;
        public static void Calculate(TrainPhysicsContext context, float deltaTimeSeconds)
        {
            context.State.signedDisplacementM = 0f;
            if (float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds) || deltaTimeSeconds < 0f)
                return;

            if (!TryApplyBrakeHold(context))
            {
                CalculateAcceleration(context);
                CalculateVelocity(context, deltaTimeSeconds);
            }
        }

        public static bool TryApplyBrakeHold(TrainPhysicsContext context)
        {
            if (context.Input.totalBrakeForceN <= 0f || 
                Mathf.Abs(context.State.signedVelocityMps) > StopThresholdMps)
            {
                return false;
            }

            float nonBrakeForceN = context.Input.NonBrakeForceN +
                CalculateRunningResistanceForceN(context.Input, context.State.signedVelocityMps);
            if (Mathf.Abs(nonBrakeForceN) > context.Input.totalBrakeForceN)
            {
                return false;
            }

            context.State.signedVelocityMps = 0;
            context.State.signedAcceleration = 0;
            context.State.signedDisplacementM = 0;

            return true;
        }

        public static void CalculateAcceleration(TrainPhysicsContext context)
        {
            float nonBrakeForceN = context.Input.NonBrakeForceN +
                CalculateRunningResistanceForceN(context.Input, context.State.signedVelocityMps);
            float brakeDirectionSource =
                context.State.signedVelocityMps != 0f
                    ? context.State.signedVelocityMps
                    : nonBrakeForceN;

            float signedBrakeForceN =
                Mathf.Abs(brakeDirectionSource) > 0.001f
                    ? -Mathf.Sign(brakeDirectionSource) *
                    context.Input.totalBrakeForceN
                    : 0f;
                
            float netForceN = nonBrakeForceN + signedBrakeForceN;
            
            context.State.signedAcceleration = netForceN / Mathf.Max(0.001f, context.Input.totalMassKg);
        }

        public static float CalculateRunningResistanceForceN(TrainPhysicsInput input, float signedVelocityMps)
        {
            float speedMps = Mathf.Abs(signedVelocityMps);
            if (speedMps > 0f)
            {
                float resistanceN = input.totalRunningResistanceAN +
                    input.totalRunningResistanceBNsPerM * speedMps +
                    input.totalRunningResistanceCNs2PerM2 * speedMps * speedMps;
                return -Mathf.Sign(signedVelocityMps) * resistanceN;
            }

            // 停止中の定数項は駆動力・勾配力などを超えて列車を動かさない。
            float forceWithoutResistanceN = input.NonBrakeForceN;
            return -Mathf.Sign(forceWithoutResistanceN) *
                Mathf.Min(Mathf.Abs(forceWithoutResistanceN), input.totalRunningResistanceAN);
        }

        public static float CalculateGradeForceN(float massKg, float gradientPermille)
        {
            // 線路サンプルの勾配は編成前方向の高さの変化 [‰]。
            float slope = gradientPermille / 1000f;
            return -massKg * GravityMps2 * slope / Mathf.Sqrt(1f + slope * slope);
        }

        public static void CalculateVelocity(TrainPhysicsContext context, float deltaTimeSeconds)
        {
            float currentVelocityMps = context.State.signedVelocityMps;
            float nextVelocityMps = currentVelocityMps + context.State.signedAcceleration * deltaTimeSeconds;

            if (
                (currentVelocityMps > 0f && nextVelocityMps < 0f) ||
                (currentVelocityMps < 0f && nextVelocityMps > 0f)
            )
            {
                float timeToStopSeconds = -currentVelocityMps / context.State.signedAcceleration;
                context.State.signedDisplacementM = 0.5f * currentVelocityMps * timeToStopSeconds;
                nextVelocityMps = 0;
            }
            else
            {
                context.State.signedDisplacementM =
                    0.5f * (currentVelocityMps + nextVelocityMps) * deltaTimeSeconds;
            }

            context.State.signedVelocityMps = nextVelocityMps;
        }

        public static void CaptureOutPut(TrainPhysicsContext context)
        {
            context.Output.signedVelocityMps = context.State.signedVelocityMps;
            context.Output.signedAcceleration = context.State.signedAcceleration;
            context.Output.signedDisplacementM = context.State.signedDisplacementM;
        }
    }
}
