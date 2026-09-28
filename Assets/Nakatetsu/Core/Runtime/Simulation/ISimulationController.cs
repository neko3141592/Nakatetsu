namespace Nakatetsu.Core.Simulation
{
    public interface ISimulationController
    {
        void Calculate(float deltaTimeSeconds);

        void ApplyOutput(float deltaTimeSeconds);
    }
}
