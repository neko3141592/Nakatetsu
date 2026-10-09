using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    public sealed class TrackAtcInput
    {
        // 車上と共通のシミュレーション時刻。未取得ならNaNのままにする。
        public double simulationTimeSeconds = double.NaN;

        // キーがない回路は占有状態を取得できていない。
        public Dictionary<string, bool> OccupiedByCircuitId = new();

        // 連動進路IDをキーとする。確認済みの未設定も登録し、取得失敗と区別する。
        public Dictionary<string, TrackAtcRouteInput> RoutesById = new();

        // 初回のEdge選択に使う線路接続の照合結果。未登録は接続入力の未取得。
        public Dictionary<string, bool> PhysicalPathAvailableByAtcEdgeId = new();
    }

    public sealed class TrackAtcRouteInput
    {
        // 初期化済みの連動で、この進路の設定中の状態を取得できたか。
        // falseは確認済みの未設定。入力取得失敗はRoutesByIdへの未登録で表す。
        public bool IsRouteSet;
        public bool ProceedAllowed;
        public bool PathEstablished;
        public bool RouteLocked;
        public bool CancelPending;
        public OverrunProtectionMode OverrunMode;
    }
}
