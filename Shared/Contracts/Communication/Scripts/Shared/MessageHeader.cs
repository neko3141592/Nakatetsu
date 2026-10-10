#nullable enable

namespace Nakatetsu.Contracts.Communication
{
    /// <summary>通信メッセージに共通する識別情報。</summary>
    public sealed class MessageHeader
    {
        public int SchemaVersion { get; set; } = 1;
        public string SessionId { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
    }
}
