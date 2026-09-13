namespace Nakatetsu.Train.Simulation.Orchestration.Interfaces
{
    public interface ISimulationController
    {
        void CollectInput();

        void Calculate(float deltaTimeSeconds);

        void ApplyOutput(float deltaTimeSeconds);
    }
}
