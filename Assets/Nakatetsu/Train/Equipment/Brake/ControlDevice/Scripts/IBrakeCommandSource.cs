namespace Nakatetsu.Train.Equipment.Brake.ControlDevice
{
    public interface IBrakeCommandSource
    {
        bool TryGetTargetBrakeForceN(out float targetBrakeForceN);
    }
}
