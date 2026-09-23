using Nakatetsu.Track.Simulation.Connection;
using Nakatetsu.Train.Simulation.TrackPosition;
using UnityEngine;

namespace Nakatetsu.Train.Debugging
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Nakatetsu/Train/Debug/Track Position Initializer")]
    public sealed class TrainTrackPositionDebugInitializer : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private string startEdgeId;
        [SerializeField] private float startDistanceOnEdgeM;
        [SerializeField] private bool startFrontFacesAtoB = true;

        private void Start()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>();
            }

            if (trainRoot == null || string.IsNullOrEmpty(startEdgeId))
            {
                return;
            }

            var trackPosition = trainRoot.GetComponentInChildren<TrainTrackPositionController>(true);
            if (trackPosition == null || !string.IsNullOrEmpty(trackPosition.Context.State.currentEdgeId))
            {
                return;
            }

            var graph = trackPosition.TrackGraphController;
            if (graph == null || !graph.IsInitialized)
            {
                return;
            }

            // 接続状態を先に用意し、初期位置から編成全体の経路を作れるようにする。
            if (graph.TryGetComponent<TrackConnectionController>(out var connections) &&
                !connections.IsInitialized && !connections.TryInitialize(out _))
            {
                return;
            }

            trackPosition.SetTrackPosition(startEdgeId, startDistanceOnEdgeM, startFrontFacesAtoB);
        }
    }
}
