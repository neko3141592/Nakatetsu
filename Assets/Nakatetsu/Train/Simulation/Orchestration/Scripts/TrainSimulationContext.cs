using System.Collections.Generic;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    public sealed class TrainCarSimulationInput
    {
        public int carIndex;
        public float massKg;

        // 編成前方を正
        public float tractionForceN;

        // 非負の大きさ
        public float brakeForceN;

        // 編成前方を正
        public float externalForceN;
    }

    public sealed class TrainSimulationInput
    {
        public readonly List<TrainCarSimulationInput> cars = new();
    }
    public sealed class TrainSimulationOutput
    {
        
    }
    public sealed class TrainSimulationContext
    {
        public TrainSimulationInput Input { get; }
        public TrainSimulationOutput Output { get; }
    }
}