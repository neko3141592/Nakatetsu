#nullable enable

using MessagePack;

namespace Nakatetsu.Contracts.Communication
{
    /// <summary>通信メッセージに共通する識別情報。</summary>
    [MessagePackObject]
    public sealed class MessageHeader
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public string SessionId { get; set; } = string.Empty;

        [Key(2)]
        public string MessageType { get; set; } = string.Empty;
    }
}
