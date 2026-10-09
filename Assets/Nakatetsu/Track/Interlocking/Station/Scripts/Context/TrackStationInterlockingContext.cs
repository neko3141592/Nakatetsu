namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackStationInterlockingContext
    {
        public TrackStationInterlockingInput Input { get; } = new();
        public TrackStationInterlockingSettings Settings { get; internal set; } = new();
        public TrackStationInterlockingState State { get; } = new();
        public TrackStationInterlockingOutput Output { get; internal set; } = new();
    }
}
