using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;

namespace Nakatetsu.Track.Atc
{
    public sealed class TrackAtcValidationState
    {
        // 今回のGraphを経路探索に使用できるか。初期値は未確認のfalse。
        public bool isGraphValid;

        // 今回のシミュレーション時刻を電文の発行時刻に使用できるか。
        // 初期値は未確認のfalse。有限かつ0以上であることを確認する。
        public bool isSimulationTimeValid;

        // 確認済みの静的定義をIDから参照する。初期状態は空。
        // 定義自体は書き換えず、Graphが不正な場合は探索に使用しない。
        public Dictionary<string, TrackAtcGraphEdge> atcEdgesById = new();
        public Dictionary<string, TrackAtcGraphNode> atcNodesById = new();

        // キーはATC進路ID。Inputで使用する連動進路IDとは区別する。
        public Dictionary<string, TrackAtcRouteDefinition> atcRoutesById = new();

        // 進路の設定状態に依存しない、Edgeごとの経路探索に使用できる方向。
        // BlockはEdge定義、Interlockingは進路の進入方向とEdge列から求め、重複を除く。
        // 進路未設定時の単一Edge・Noneの双方向停止結果は、工程3で別途生成する。
        public Dictionary<string, HashSet<TrackEdgeTravelDirection>> directionsByAtcEdgeId = new();

        // キーは軌道回路ID。trueは占有入力を取得できたことを表し、空きを意味しない。
        // falseまたは未登録は入力未取得・未確認として扱う。
        public Dictionary<string, bool> hasCircuitInputById = new();

        // キーは連動進路ID。確認済みの進路未設定もtrueとし、取得失敗と区別する。
        // falseまたは未登録は入力未取得・未確認として扱う。
        public Dictionary<string, bool> hasRouteInputById = new();

        // Graphや時刻を使用できない理由。個別の入力欠落は上の取得可否に保持する。
        // 初期値は空。理由が空であることだけでは、確認済み・正常と判断しない。
        public string failureReason = string.Empty;
    }
}
