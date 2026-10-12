#nullable enable

using System.Collections.Generic;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>列車計画をまとめて保存するダイヤ。</summary>
    public sealed class TimetableEntity
    {
        /// <summary>DBが生成するダイヤの内部ID。</summary>
        public long TimetableId { get; set; }

        /// <summary>表示するダイヤ名。</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>子データを含む編集ごとに増やす、同時編集の検出用リビジョン。</summary>
        public long Revision { get; set; }

        /// <summary>ダイヤに含まれる列車計画。</summary>
        public ICollection<TimetableTrainEntity> Trains { get; set; } = new List<TimetableTrainEntity>();

        /// <summary>ダイヤに含まれる編成の運用。</summary>
        public ICollection<TimetableOperationEntity> Operations { get; set; } = new List<TimetableOperationEntity>();
    }
}
