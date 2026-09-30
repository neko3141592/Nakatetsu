using Nakatetsu.Core.Simulation;

namespace Nakatetsu.Train.Equipment.Shared
{
    public interface IEquipmentController : ISimulationController
    {
        void CollectInput();
    }
}
