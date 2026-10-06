using System.Collections.Generic;

namespace Nakatetsu.Track.NewAtc
{
    public sealed class TrackAtcTelegramState
    {
        // 軌道回路IDごとの今回の送信対象。工程5で毎更新置き換える。
        public Dictionary<string, TrackAtcTelegramCircuitResult> circuitsById = new();
    }

    public sealed class TrackAtcTelegramCircuitResult
    {
        // 経路・停止限界・方式は所有するStateを参照し、ここでは複製しない。
        public HashSet<TrackAtcEdgeKey> routeKeys = new();

        // 初期値は未確定のfalse。工程5で確定し、同じ更新内の失敗で無効にした
        // 回路は、ほかの経路が成功しても有効へ戻さない。
        public bool isValid;
        public HashSet<string> failureReasons = new();
    }
}
