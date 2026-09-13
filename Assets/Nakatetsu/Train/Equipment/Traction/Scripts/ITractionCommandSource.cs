namespace Nakatetsu.Train.Equipment.Traction
{
    public interface ITractionCommandSource
    {
        bool TryGetTargetTractionForceN(out float targetTractionForceN);
    }
}
