using Nakatetsu.Track.Atc;

namespace Nakatetsu.Track.NewAtc
{
    public sealed class TrackAtcContext
    {
        // コンパイル済みの静的定義。計算中には書き換えない。
        public TrackAtcGraphDefinition Graph { get; set; }

        public TrackAtcInput Input { get; } = new();
        public TrackAtcState State { get; } = new();
        public TrackAtcOutput Output { get; } = new();
    }
}
