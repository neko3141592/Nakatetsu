using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Simulation.Atc
{
    public sealed class TrainAtcReceiverInput
    {
        public bool hasEdgePosition;
        public string edgeId;
        public float distanceOnEdgeM;
    }

    public sealed class TrainAtcReceiverOutput
    {
        public bool hasEdgePosition;
        public string edgeId;
        public float distanceOnEdgeM;
        public TrackCircuitAtcTelegram telegram;
    }

    public sealed class TrainAtcReceiverContext
    {
        public TrainAtcReceiverInput Input { get; } = new();
        public TrainAtcReceiverOutput Output { get; } = new();
    }
}
