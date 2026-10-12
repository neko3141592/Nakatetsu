#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤから参照する進路IDの設備カタログ。</summary>
    public sealed class RouteEntity
    {
        /// <summary>進路を管理する連動制御駅のID。ダイヤ上の駅IDとは別に扱う。</summary>
        public string ControlStationId { get; set; } = string.Empty;

        /// <summary>連動制御駅内の進路ID。制御駅IDと合わせて主キーとなる。</summary>
        public string RouteId { get; set; } = string.Empty;
    }
}
