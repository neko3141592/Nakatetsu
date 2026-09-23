using UnityEngine;

namespace Nakatetsu.Train.Simulation.Physics
{
    public static class TrainPhysicsLogic
    {
        private const float StopThresholdMps = 0.01f;
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

            if (Mathf.Abs(context.Input.NonBrakeForceN) > context.Input.totalBrakeForceN)
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
            float brakeDirectionSource =
                Mathf.Abs(context.State.signedVelocityMps) > StopThresholdMps
                    ? context.State.signedVelocityMps
                    : context.Input.NonBrakeForceN;

            float signedBrakeForceN =
                Mathf.Abs(brakeDirectionSource) > 0.001f
                    ? -Mathf.Sign(brakeDirectionSource) *
                    context.Input.totalBrakeForceN
                    : 0f;
                
            float netForceN = 
                context.Input.totalTractionForceN 
                + context.Input.totalExternalForceN 
                + signedBrakeForceN;
            
            context.State.signedAcceleration = netForceN / Mathf.Max(0.001f, context.Input.totalMassKg);
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
