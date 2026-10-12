using Nakatetsu.Contracts.Communication;

namespace Nakatetsu.TrafficControl.Server.CTC
{
    public sealed class CTCContext
    {
        public Message<UnitySnapshot>? Input { get; internal set; }
        public Message<UnitySnapshot>? Output { get; internal set; }
    }
}
