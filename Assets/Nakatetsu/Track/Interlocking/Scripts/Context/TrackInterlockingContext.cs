namespace Nakatetsu.Track.Interlocking
{
    public sealed class TrackInterlockingContext
    {
        public TrackInterlockingInput Input { get; } = new();
        public TrackInterlockingSettings Settings { get; internal set; } = new();
        public TrackInterlockingState State { get; } = new();
        public TrackInterlockingOutput Output { get; internal set; } = new();
    }
}
