namespace Nakatetsu.Train.Simulation.Physics
{
    public sealed class TrainPhysicsInput
    {
        public float totalMassKg;
        public float totalTractionForceN;
        public float totalBrakeForceN;
        public float totalExternalForceN;

        public float NonBrakeForceN => totalExternalForceN + totalTractionForceN;

        public void Reset()
        {
            totalMassKg = 0f;
            totalTractionForceN = 0f;
            totalBrakeForceN = 0f;
            totalExternalForceN = 0f;
        }
    }

    public sealed class TrainPhysicsState
    {
        //
        public float signedAcceleration;
        // 符号付き速度
        public float signedVelocityMps;
    }


    public sealed class TrainPhysicsOutput
    {
        // 符号付き加速度
        public float signedAcceleration;
        // 符号付き速度
        public float signedVelocityMps;
    }

    public sealed class TrainPhysicsContext
    {
        public TrainPhysicsInput Input { get; } = new();
        public TrainPhysicsState State { get; } = new();
        public TrainPhysicsOutput Output { get; } = new();
    }
}
