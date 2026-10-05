using System.Collections.Generic;
using Nakatetsu.Track.Atc;

namespace Nakatetsu.Train.Equipment.Atc
{
    public class TrainAtcContext
    {
        public TrainAtcSettings Settings { get; } = new();

        // 地上装置の状態ではなく、車上計算用の路線定義を参照する。
        public TrackAtcGraphDefinition Graph { get; internal set; }
        public readonly Dictionary<string, TrackAtcGraphEdge> atcEdgesById = new();

        public TrainAtcInput Input { get; } = new();
        public TrainAtcState State { get; } = new();
        public TrainAtcOutput Output { get; } = new();
    }
}
