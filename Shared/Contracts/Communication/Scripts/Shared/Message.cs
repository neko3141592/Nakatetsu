#nullable enable

namespace Nakatetsu.Contracts.Communication
{
    /// <summary>共通ヘッダーと送信データをまとめる通信メッセージ。</summary>
    public sealed class Message<TPayload> where TPayload : class, new()
    {
        public MessageHeader Header { get; set; } = new MessageHeader();
        public TPayload Payload { get; set; } = new TPayload();
    }
}
