#nullable enable

using MessagePack;

namespace Nakatetsu.Contracts.Communication
{
    /// <summary>共通ヘッダーと送信データをまとめる通信メッセージ。</summary>
    [MessagePackObject]
    public sealed class Message<TPayload> where TPayload : class, new()
    {
        [Key(0)]
        public MessageHeader Header { get; set; } = new MessageHeader();

        [Key(1)]
        public TPayload Payload { get; set; } = new TPayload();
    }
}
