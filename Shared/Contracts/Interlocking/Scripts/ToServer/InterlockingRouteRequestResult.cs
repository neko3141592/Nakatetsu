#nullable enable

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>進路要求の受付結果。開通・解錠の完了は進路状態で確認する。</summary>
    public sealed class InterlockingRouteRequestResult
    {
        public string RequestId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;

        // 受付時はtrueとNone、拒否時はfalseとNone以外の理由を設定する。
        public bool Accepted { get; set; }
        public InterlockingRouteErrorCode ErrorCode { get; set; } = InterlockingRouteErrorCode.InvalidRequest;

        public string? RelatedRouteId { get; set; }
        public string? RelatedCircuitId { get; set; }

        // 既存の駅連動装置では転轍機をConnectionIdで識別する。
        public string? RelatedTurnoutId { get; set; }

        // 表示・ログ向けの補足。処理の分岐にはErrorCodeを使用する。
        public string? Message { get; set; }
    }
}
