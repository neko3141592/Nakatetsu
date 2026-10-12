#nullable enable

using System.Collections.Generic;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>同じ編成が順番に担当する列車をまとめる運用。</summary>
    public sealed class TimetableOperationEntity
    {
        /// <summary>この運用を保存するダイヤのID。</summary>
        public long TimetableId { get; set; }

        /// <summary>ダイヤ内で運用を一意に識別する番号。</summary>
        public string OperationNumber { get; set; } = string.Empty;

        /// <summary>運用に含まれる列車。使用順はSequenceIndexで扱う。</summary>
        public ICollection<TimetableTrainEntity> Trains { get; set; } = new List<TimetableTrainEntity>();
    }
}
