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
        private TrainTrackConnectionResolver connectionResolver;

        private TrainTrackConnectionResolver ConnectionResolver => connectionResolver ??= TryResolveNextEdge;

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
            context.Workspace.ResetPath();
        }

        public void SetInput(float signedDisplacementM)
        {
            context.Input.signedDisplacementM = signedDisplacementM;
        }

        private bool TryResolveNextEdge(string nodeId, string incomingEdgeId, out string nextEdgeId)
        {
            nextEdgeId = null;
            return trackGraphController != null &&
                trackGraphController.TryGetComponent<TrackConnectionController>(out var connections) &&
                connections.TryResolveNextEdge(nodeId, incomingEdgeId, out nextEdgeId, out _);
        }

        public bool TryConfigureConsist()
        {
            var root = GetComponentInParent<TrainRoot>();
            var definition = root != null ? root.ConsistDefinition : null;
            if (definition == null || definition.CarCount == 0)
            {
                return false;
            }

            var lengths = new float[definition.CarCount];
            for (int i = 0; i < lengths.Length; i++)
            {
                if (definition.cars[i] == null)
                {
                    return false;
                }

                lengths[i] = definition.cars[i].lengthM;
                if (!TrainTrackPathLogic.IsFinite(lengths[i]) || lengths[i] <= 0f)
                {
                    return false;
                }
            }

            TrainTrackSamplingLogic.ConfigureLayout(context, lengths);
            return true;
        }

        // 固定先頭車の中心を配置する。再配置時は経路履歴もリセットする。
        public void SetTrackPosition(string edgeId, float distanceOnEdgeM, bool frontFacesAtoB)
        {
            context.State.currentEdgeId = edgeId;
            context.State.distanceOnEdgeM = distanceOnEdgeM;
            context.State.frontFacesAtoB = frontFacesAtoB;
            context.Input.signedDisplacementM = 0f;
            context.Workspace.ResetPath();

            Calculate(0f);
        }

        // 車両Indexは0始まり。オフセット[m]は編成の固定前方向が正。
        public bool TryGetTrackSample(int carIndex, float offsetFromCarCenterM, out TrainTrackSample sample)
        {
            sample = default;
            return TryGetGraphContext(out var graph) &&
                TrainTrackSamplingLogic.TryGetTrackSample(context, graph, carIndex, offsetFromCarCenterM, out sample);
        }

        public void Calculate(float deltaTimeSeconds)
        {
            if (context.Settings.CarCount == 0 && !TryConfigureConsist())
            {
                return;
            }

            if (TryGetGraphContext(out var graph))
            {
                TrainTrackPositionLogic.Calculate(context, graph, ConnectionResolver);
            }
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
        }
    }
}
