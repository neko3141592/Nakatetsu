#nullable enable

using System.Collections.Generic;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤに含まれる1本の列車計画。</summary>
    public sealed class TimetableTrainEntity
    {
        /// <summary>この列車計画を保存するダイヤのID。</summary>
        public long TimetableId { get; set; }

        /// <summary>ダイヤ内で列車計画を一意に識別する列番。</summary>
        public string TrainNumber { get; set; } = string.Empty;

        /// <summary>この列車計画が所属する運用番号。</summary>
        public string OperationNumber { get; set; } = string.Empty;

        /// <summary>運用内の使用順。0始まり。</summary>
        public int SequenceIndex { get; set; }

        /// <summary>列車種別マスターのID。</summary>
        public string TrainTypeId { get; set; } = string.Empty;

        /// <summary>列車計画に含まれる地点。順序はSequenceIndexで扱う。</summary>
        public ICollection<TimetablePointEntity> Points { get; set; } = new List<TimetablePointEntity>();
    }
}
