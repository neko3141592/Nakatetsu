using Nakatetsu.Track.Graph;
using UnityEngine;

namespace Nakatetsu.Track.Simulation.Connection
{
    /// <summary>Graphと同じGameObjectに配置し、全列車・連動装置が共有する。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrackGraphController))]
    public sealed class TrackConnectionController : MonoBehaviour
    {
        [SerializeField] private TrackSwitchPosition initialPosition = TrackSwitchPosition.Normal;
        [Tooltip("有効なら要求受付時に転換完了。無効なら動作側からTryConfirmPositionを呼ぶ。")]
        [SerializeField] private bool completeRequestsImmediately = true;

        private TrackGraphController trackGraphController;
        private readonly TrackConnectionContext context = new();

        public bool IsInitialized => context.IsInitialized;

        private void Awake()
        {
            trackGraphController = GetComponent<TrackGraphController>();
        }

        private void Start()
        {
            // GraphのAwakeによる初期化が終わってから状態を生成する。
            if (!IsInitialized && !TryInitialize(out string error))
                Debug.LogError($"Track connection initialization failed: {error}", this);
        }

        /// <summary>設定した初期位置へ全転轍機をリセットする。実行中の通常操作には使わない。</summary>
        public bool TryInitialize(out string error)
        {
            if (trackGraphController == null)
                trackGraphController = GetComponent<TrackGraphController>();
            return TrackConnectionLogic.TryInitialize(context,
                trackGraphController != null ? trackGraphController.Context : null,
                initialPosition, out error);
        }

        /// <summary>連動条件の判定を終えた要求を受け付ける。進路鎖錠・占有判定は連動装置側の責務。</summary>
        public bool TryRequestPosition(string connectionId, TrackSwitchPosition position, out string error)
        {
            if (!TrackConnectionLogic.TryRequestPosition(context, connectionId, position, out error))
                return false;
            if (completeRequestsImmediately)
                return TrackConnectionLogic.TryConfirmPosition(context, connectionId, position, out error);
            return true;
        }

        /// <summary>転換動作の完了通知。受付済みの要求位置と一致するときに確定する。</summary>
        public bool TryConfirmPosition(string connectionId, TrackSwitchPosition position, out string error) =>
            TrackConnectionLogic.TryConfirmPosition(context, connectionId, position, out error);

        public bool TryGetState(string connectionId, out TrackConnectionState state) =>
            context.TryGetState(connectionId, out state);

        public bool TryResolveNextEdge(string nodeId, string incomingEdgeId,
            out string nextEdgeId, out string error) =>
            TrackConnectionLogic.TryResolveNextEdge(context, nodeId, incomingEdgeId, out nextEdgeId, out error);
    }
}
