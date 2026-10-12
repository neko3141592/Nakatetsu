#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤから参照する軌道回路IDの設備カタログ。</summary>
    public sealed class TrackCircuitEntity
    {
        /// <summary>対象設備全体で一意の軌道回路ID。</summary>
        public string TrackCircuitId { get; set; } = string.Empty;
    }
}
