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

        // 編成前方を正。下り勾配では前方への力となる。
        public float gradeForceN;

        // R = A + B |v| + C |v|²。いずれも非負の係数。
        public float runningResistanceAN;
        public float runningResistanceBNsPerM;
        public float runningResistanceCNs2PerM2;
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
        public TrainSimulationInput Input { get; } = new();
        public TrainSimulationOutput Output { get; } = new();
    }
}
