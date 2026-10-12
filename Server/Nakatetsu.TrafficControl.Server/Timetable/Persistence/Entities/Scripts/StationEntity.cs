#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤ上の駅マスター。</summary>
    public sealed class StationEntity
    {
        /// <summary>ダイヤ上の駅の文字列ID。</summary>
        public string StationId { get; set; } = string.Empty;

        /// <summary>表示する駅名。</summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
