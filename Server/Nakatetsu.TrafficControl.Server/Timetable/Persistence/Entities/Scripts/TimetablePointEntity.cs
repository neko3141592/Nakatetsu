#nullable enable

using System.Collections.Generic;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>列車計画に含まれる停車・通過地点と計画時刻。</summary>
    public sealed class TimetablePointEntity
    {
        /// <summary>DBが生成するダイヤ地点の内部ID。</summary>
        public long TimetablePointId { get; set; }

        /// <summary>この地点を含むダイヤのID。</summary>
        public long TimetableId { get; set; }

        /// <summary>この地点を含む列車計画の列番。</summary>
        public string TrainNumber { get; set; } = string.Empty;

        /// <summary>列車計画内の地点の通過順。0始まり。</summary>
        public int SequenceIndex { get; set; }

        /// <summary>ダイヤ上の駅ID。駅以外の地点はNULL。</summary>
        public string? StationId { get; set; }

        /// <summary>この地点に対応する軌道回路ID。</summary>
        public string TrackCircuitId { get; set; } = string.Empty;

        /// <summary>停車または通過の扱い。保存時に必ず指定する。</summary>
        public TimetablePointHandling Handling { get; set; }

        /// <summary>運行日0時からの到着秒数。24時以降も許可し、未入力はNULL。</summary>
        public int? ArrivalSeconds { get; set; }

        /// <summary>運行日0時からの出発・通過秒数。24時以降も許可し、未入力はNULL。</summary>
        public int? DepartureSeconds { get; set; }

        /// <summary>この地点から使用する進路。順序はSequenceIndexで扱う。</summary>
        public ICollection<TimetablePointRouteEntity> Routes { get; set; } = new List<TimetablePointRouteEntity>();
    }
}
