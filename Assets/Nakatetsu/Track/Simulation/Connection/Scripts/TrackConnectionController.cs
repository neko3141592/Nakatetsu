using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Track.Graph;
using UnityEngine;
using UnityEngine.Serialization;

namespace Nakatetsu.Track.Simulation.Connection
{
    // Graphと同じGameObjectに配置し、全列車と連動装置で共有する。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrackGraphController))]
    public sealed class TrackConnectionController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrackSwitchPosition initialPosition = TrackSwitchPosition.Normal;
        [Tooltip("有効なら転轍時間の経過で自動確定。無効なら動作側からTryConfirmPositionを呼ぶ。")]
        [FormerlySerializedAs("completeRequestsImmediately")]
        [SerializeField] private bool completeRequestsAutomatically = true;
        [Tooltip("シミュレーション時間での転轍秒数。0なら要求受付時に完了する。")]
        [SerializeField, Min(0f)] private float switchDurationSeconds = 3f;

        private TrackGraphController trackGraphController;
        private readonly TrackConnectionContext context = new();

        public TrackConnectionContext Context => context;
        public bool IsInitialized => context.IsInitialized;

        private void Awake()
        {
            trackGraphController = GetComponent<TrackGraphController>();
        }

        private void Start()
        {
            // GraphのAwakeによる初期化が終わってから状態を生成する。
            if (!IsInitialized && !TryInitialize(out string error))
            {
                Debug.LogError($"Track connection initialization failed: {error}", this);
            }
        }

        // 全転轍機を初期位置へ戻す。走行中の通常操作には使わない。
        public bool TryInitialize(out string error)
        {
            if (trackGraphController == null)
            {
                trackGraphController = GetComponent<TrackGraphController>();
            }

            return TrackConnectionLogic.TryInitialize(context,
                trackGraphController != null ? trackGraphController.Context : null,
                initialPosition, out error);
        }

        // 連動条件を判定済みの転換要求を受け付ける。
        public bool TryRequestPosition(string connectionId, TrackSwitchPosition position, out string error) =>
            TrackConnectionLogic.TryRequestPosition(context, connectionId, position,
                completeRequestsAutomatically ? switchDurationSeconds : (float?)null, out error);

        public void Calculate(float deltaTimeSeconds) =>
            TrackConnectionLogic.Calculate(context, deltaTimeSeconds);

        public void ApplyOutput(float deltaTimeSeconds) { }

        // 受付済みの要求位置と一致する転換完了だけを確定する。
        public bool TryConfirmPosition(string connectionId, TrackSwitchPosition position, out string error) =>
            TrackConnectionLogic.TryConfirmPosition(context, connectionId, position, out error);

        public bool TryGetState(string connectionId, out TrackConnectionState state) =>
            context.TryGetState(connectionId, out state);

        // 未初期化は未取得。確定位置と転換中の状態を、その時点の値としてコピーする。
        public IReadOnlyDictionary<string, TrackConnectionState> CaptureStates() =>
            !IsInitialized ? null : new ReadOnlyDictionary<string, TrackConnectionState>(
                new Dictionary<string, TrackConnectionState>(context.StatesById));

        public bool TryResolveNextEdge(string nodeId, string incomingEdgeId,
            out string nextEdgeId, out string error) =>
            TrackConnectionLogic.TryResolveNextEdge(context, nodeId, incomingEdgeId, out nextEdgeId, out error);
    }
}
