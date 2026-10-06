using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.NewAtc
{
    public sealed class TrackAtcOutput
    {
        // 軌道回路ID -> 今回の電文。送信対象が空の場合も前回の内容を消去する。
        public Dictionary<string, TrackCircuitAtcTelegram> telegrams = new();
    }
}
