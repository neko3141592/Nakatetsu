namespace Nakatetsu.Train.Simulation.TrackPosition
{
    public sealed class TrainTrackPositionInput
    {
        // Requested travel along the consist's fixed front direction during this tick.
        public float signedDisplacementM;
    }

    public sealed class TrainTrackPositionState
    {
        // Position of the fixed leading car center on the current Edge.
        public string currentEdgeId;
        public float distanceOnEdgeM;
        // True when the consist's fixed front faces from Node A toward Node B.
        public bool frontFacesAtoB;
    }

    public sealed class TrainTrackPositionContext
    {
        public TrainTrackPositionInput Input { get; } = new();
        public TrainTrackPositionState State { get; } = new();
    }
}
