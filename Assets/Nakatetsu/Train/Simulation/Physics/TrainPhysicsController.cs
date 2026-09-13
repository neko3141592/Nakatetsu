using System.Collections.Generic;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Physics
{
    public sealed class TrainPhysicsController : MonoBehaviour, ISimulationController
    {
        private readonly TrainPhysicsContext context = new();
        private IReadOnlyList<TrainCarSimulationInput> inputCars;

        public TrainPhysicsContext Context => context;

        public void SetInputSource(IReadOnlyList<TrainCarSimulationInput> cars)
        {
            inputCars = cars;
        }

        public void CollectInput()
        {
            TrainPhysicsInput input = context.Input;
            input.Reset();
            if (inputCars == null)
            {
                return;
            }

            foreach (TrainCarSimulationInput car in inputCars)
            {
                if (car == null)
                {
                    continue;
                }

                input.totalMassKg += Mathf.Max(0f, car.massKg);
                input.totalTractionForceN += car.tractionForceN;
                input.totalBrakeForceN += Mathf.Max(0f, car.brakeForceN);
                input.totalExternalForceN += car.externalForceN;
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
