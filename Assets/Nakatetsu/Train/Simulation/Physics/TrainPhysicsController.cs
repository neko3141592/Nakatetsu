using System.Collections.Generic;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Physics
{
    public sealed class TrainPhysicsController : MonoBehaviour, ISimulationController
    {
        private readonly TrainPhysicsContext context = new();

        public TrainPhysicsContext Context => context;

        public void SetInput(IReadOnlyList<TrainCarSimulationInput> cars)
        {
            TrainPhysicsInput input = context.Input;
            input.Reset();
            if (cars == null)
            {
                return;
            }

            foreach (TrainCarSimulationInput car in cars)
            {
                if (car == null)
                {
                    continue;
                }

                input.totalMassKg += Mathf.Max(0f, car.massKg);
                input.totalTractionForceN += car.tractionForceN;
                input.totalBrakeForceN += Mathf.Max(0f, car.brakeForceN);
                input.totalExternalForceN += car.externalForceN;
                input.totalGradeForceN += car.gradeForceN;
                input.totalRunningResistanceAN += Mathf.Max(0f, car.runningResistanceAN);
                input.totalRunningResistanceBNsPerM += Mathf.Max(0f, car.runningResistanceBNsPerM);
                input.totalRunningResistanceCNs2PerM2 += Mathf.Max(0f, car.runningResistanceCNs2PerM2);
            }
        }

        public void Calculate(float deltaTimeSeconds)
        {
            TrainPhysicsLogic.Calculate(context, deltaTimeSeconds);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            TrainPhysicsLogic.CaptureOutPut(context);
        }
    }
}
