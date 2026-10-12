namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤ地点での停車・通過の扱い。未指定の0は保存できない。</summary>
    public enum TimetablePointHandling
    {
        /// <summary>停車地点。</summary>
        Stop = 1,

        /// <summary>通過地点。</summary>
        Pass = 2,
    }
}
