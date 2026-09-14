namespace Nakatetsu.Train.Equipment.Shared
{
    public interface IEquipmentController
    {
        void CollectInput();

        void Calculate(float deltaTimeSeconds);

        void ApplyOutput(float deltaTimeSeconds);
    }
}
