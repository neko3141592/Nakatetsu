#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>普通・快速・回送などの列車種別マスター。</summary>
    public sealed class TrainTypeEntity
    {
        /// <summary>列車種別の文字列ID。</summary>
        public string TrainTypeId { get; set; } = string.Empty;

        /// <summary>表示する種別名。</summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
