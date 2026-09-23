using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Connection;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    [DisallowMultipleComponent]
    public sealed class TrainTrackPositionController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrackGraphController trackGraphController;
        private readonly TrainTrackPositionContext context = new();

        public TrackGraphController TrackGraphController => trackGraphController;
        public TrainTrackPositionContext Context => context;

        public bool TryGetGraphContext(out TrackGraphContext context)
        {
            context = trackGraphController != null && trackGraphController.IsInitialized
                ? trackGraphController.Context
                : null;
            return context != null;
        }

        public void SetTrackGraphController(TrackGraphController controller)
        {
            trackGraphController = controller;
        }

        public void SetInput(float signedDisplacementM)
        {
            context.Input.signedDisplacementM = signedDisplacementM;
        }

        /// <summary>Graphと同じ設備Controllerから、実際の転轍機位置に対応する次Edgeを取得する。</summary>
        public bool TryResolveNextEdge(string nodeId, string incomingEdgeId,
            out string nextEdgeId, out string error)
        {
            nextEdgeId = null;
            if (trackGraphController == null ||
                !trackGraphController.TryGetComponent<TrackConnectionController>(out var connections))
            {
                error = "TrackConnectionController is not attached to the track graph.";
                return false;
            }
            return connections.TryResolveNextEdge(nodeId, incomingEdgeId, out nextEdgeId, out error);
        }

        public void Calculate(float deltaTimeSeconds)
        {
            // Edge traversal will consume context.Input.signedDisplacementM here.
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // There is no calculated track position to publish yet.
        }
    }
}
