namespace Nakatetsu.Track.Interlocking.Management
{
    public sealed class TrackInterlockingManagementContext
    {
        public TrackInterlockingManagementInput Input { get; } = new();
        public TrackInterlockingManagementState State { get; } = new();
        public TrackInterlockingManagementOutput Output { get; internal set; } = new();
    }
}
