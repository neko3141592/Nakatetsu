using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Track.Atc
{
    public sealed class TrackAtcPathState
    {
        // 探索起点のEdge ID・方向ごとの今回の計算結果。初期状態は空。
        public Dictionary<TrackAtcEdgeKey, TrackAtcPathResult> resultsByKey = new();
    }

    public sealed class TrackAtcPathResult
    {
        // 次Edge参照と停止限界を正常に確定できたか。初期値は未確認のfalse。
        public bool isPathValid;
        // 計算失敗の理由。理由が空でも、isPathValidがfalseなら使用しない。
        public string failureReason = string.Empty;

        // 次Edgeがなければnull。結果の成否はisPathValidで区別する。
        public TrackAtcEdgeKey? nextEdgeKey;

        // この起点に対する停止限界。指定した方向のEdge退出端を表す。
        // 初期状態ではIDはnull、方向はUnspecified。
        public string stopAtcEdgeId;
        public TrackEdgeTravelDirection stopTravelDirection;

        // 探索終了の理由。初期値は未判定のUnspecified。
        public TrackAtcPathEndReason endReason;
        // 停止限界に対応するATC進路ID。終端進路が対応しない場合はnull。
        public string terminalAtcRouteId;

        // 探索失敗時に無効化する軌道回路ID。初期状態・正常終了時は空。
        // 同じ回路が複数のEdgeに対応していても重複して登録しない。
        public HashSet<string> affectedCircuitIds = new();
    }

    public enum TrackAtcPathEndReason
    {
        // 初期値。まだ探索終了の理由を確定していない。
        Unspecified = 0,
        // 対応する進路が未設定と確認できた。
        RouteNotSet,
        // 進路は設定されているが、新規進入・開通・鎖錠の使用条件を満たさない。
        RouteUnavailable,
        // 取消中の進路に従った探索を継続できない。
        RouteCancelled,
        // 異なる回路へ進む際に、次回路の占有を確認した。
        NextCircuitOccupied,
        // 設定済み進路の末尾に到達し、後続進路やBlockへ進めない。
        RouteEnd,
        // 継続先のEdgeがない。対応する終端進路の有無は別に保持する。
        NoNextEdge,
        // 入力欠落・Graph不整合・循環・探索上限到達などで計算に失敗した。
        // 詳細はfailureReasonへ保持し、isPathValidをfalseとする。
        CalculationFailed
    }
}
