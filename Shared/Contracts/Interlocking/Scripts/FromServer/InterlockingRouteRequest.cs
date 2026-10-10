#nullable enable

namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>CTCから連動管理部へ渡す進路操作要求。</summary>
    public sealed class InterlockingRouteRequest
    {
        // 要求と応答を対応づける、空でない一意のID。
        public string RequestId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public InterlockingRouteOperation Operation { get; set; }
    }
}
