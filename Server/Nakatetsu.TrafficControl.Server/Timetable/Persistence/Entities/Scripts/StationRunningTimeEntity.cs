#nullable enable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>列車種別ごとの発駅から着駅までの標準所要時間。逆方向は別レコード。</summary>
    public sealed class StationRunningTimeEntity
    {
        /// <summary>所要時間の対象となる列車種別ID。</summary>
        public string TrainTypeId { get; set; } = string.Empty;

        /// <summary>出発するダイヤ上の駅ID。</summary>
        public string FromStationId { get; set; } = string.Empty;

        /// <summary>到着するダイヤ上の駅ID。</summary>
        public string ToStationId { get; set; } = string.Empty;

        /// <summary>0より大きい標準所要秒数。</summary>
        public int RunningSeconds { get; set; }
    }
}
