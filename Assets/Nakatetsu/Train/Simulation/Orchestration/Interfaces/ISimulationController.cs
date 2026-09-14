namespace Nakatetsu.Train.Simulation.Orchestration.Interfaces
{
    public interface ISimulationController
    {
        void Calculate(float deltaTimeSeconds);

        void ApplyOutput(float deltaTimeSeconds);
    }
}
