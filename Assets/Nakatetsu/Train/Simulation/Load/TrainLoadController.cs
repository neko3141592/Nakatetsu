using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Load
{
    public sealed class TrainLoadController : MonoBehaviour, ISimulationController
    {
        private readonly TrainLoadContext context = new();

        public TrainLoadContext Context => context;
        public bool IsInitialized => context.State.isInitialized;
        public int ActualPassengerCount => context.State.actualPassengerCount;
        public float ActualPassengerMassKg => context.State.actualPassengerMassKg;
        public float ActualCargoMassKg => context.State.actualCargoMassKg;
        public float ActualTotalMassKg => context.State.actualTotalMassKg;
        public float ActualSupportedMassKg => context.State.actualSupportedMassKg;

        public void Configure(
            CarDefinitionAsset definition,
            float averagePassengerMassKg = 55f)
        {
            context.Settings.emptyMassKg = definition != null
                ? Mathf.Max(0f, definition.emptyMassKg)
                : 0f;
            context.Settings.passengerCapacity = definition != null
                ? Mathf.Max(0, definition.passengerCapacity)
                : 0;
            context.Settings.averagePassengerMassKg =
                Mathf.Max(0f, averagePassengerMassKg);
        }

        public void SetPassengerCount(int value)
        {
            context.Input.passengerCount = value;
        }

        public void SetCargoMassKg(float value)
        {
            context.Input.cargoMassKg = value;
        }

        public void Calculate(float deltaTimeSeconds)
        {
            TrainLoadLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            TrainLoadLogic.ApplyOutput(context);
        }
    }
}
