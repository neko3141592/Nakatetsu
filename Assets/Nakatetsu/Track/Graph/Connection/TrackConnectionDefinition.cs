using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Graph.Connection
{
    public enum TrackConnectionCondition
    {
        Unspecified, // 未設定を検出するため
        Always, // 固定接続
        Normal, // 定位のとき接続
        Reverse // 反位のとき接続
    }

    [Serializable]
    public sealed class TrackConnectionDefinition
    {
        public string connectionId;
        public string nodeId;

        // 固定接続は1組、転轍機は定位と反位の2組を持つ。
        public List<TrackEdgePair> edgePairs = new();
    }

    [Serializable]
    public class TrackEdgePair
    {
        public string pairId;
        public string edgeAId;
        public string edgeBId;
        public TrackConnectionCondition condition;
    }
}
