#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>あるダイヤ地点から次の地点までに使用する進路。</summary>
    public sealed class TimetablePointRouteEntity
    {
        /// <summary>使用進路列の起点となるダイヤ地点のID。</summary>
        public long TimetablePointId { get; set; }

        /// <summary>地点から使用する進路の順序。0始まりで、地点IDと合わせて主キーとなる。</summary>
        public int SequenceIndex { get; set; }

        /// <summary>進路を管理する連動制御駅のID。ダイヤ上の駅IDとは別に扱う。</summary>
        public string ControlStationId { get; set; } = string.Empty;

        /// <summary>連動制御駅内の進路ID。制御駅IDと合わせて進路を識別する。</summary>
        public string RouteId { get; set; } = string.Empty;
    }
}
