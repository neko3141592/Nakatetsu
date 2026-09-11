namespace Nakatetsu.Train.Traction
{
    public interface ITractionEquipment
    {
        bool IsAvailable { get; }
        int MotorCount { get; }
        float RatedPowerW { get; }

        // Signed force: positive is toward the front of the consist.
        float ActualTractionForceN { get; }

        float GetRegenCapacityN(float vehicleSpeedMps);

        void Step(
            float targetTractionForceN,
            float vehicleSpeedMps,
            float deltaTimeSeconds);

        void ResetEquipment();
    }
}
