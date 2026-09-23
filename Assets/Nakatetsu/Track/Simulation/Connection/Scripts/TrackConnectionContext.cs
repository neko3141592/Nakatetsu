using System.Collections.Generic;
using Nakatetsu.Track.Graph.Connection;

namespace Nakatetsu.Track.Simulation.Connection
{
    public enum TrackSwitchPosition
    {
        Unknown,
        Normal,
        Reverse
    }

    /// <summary>外部へ返す状態のスナップショット。操作はControllerの要求・完了通知を通す。</summary>
    public readonly struct TrackConnectionState
    {
        public TrackSwitchPosition RequestedPosition { get; }
        public TrackSwitchPosition ActualPosition { get; }
        public bool IsMoving { get; }

        internal TrackConnectionState(TrackSwitchPosition requestedPosition,
            TrackSwitchPosition actualPosition, bool isMoving)
        {
            RequestedPosition = requestedPosition;
            ActualPosition = actualPosition;
            IsMoving = isMoving;
        }
    }

    public sealed class TrackConnectionContext
    {
        // 初期化時に定義をコピーする。実行中にAssetのリストやペアは変更しない。
        internal readonly Dictionary<string, TrackConnectionDefinition> ConnectionsByNodeId = new();
        // 固定接続は状態を持たず、転轍機だけをConnection IDで管理する。
        internal readonly Dictionary<string, TrackConnectionState> StatesById = new();

        public bool IsInitialized { get; internal set; }

        public bool TryGetState(string connectionId, out TrackConnectionState state)
        {
            state = default;
            return IsInitialized && !string.IsNullOrWhiteSpace(connectionId) &&
                StatesById.TryGetValue(connectionId, out state);
        }

        internal void Clear()
        {
            IsInitialized = false;
            ConnectionsByNodeId.Clear();
            StatesById.Clear();
        }
    }
}
