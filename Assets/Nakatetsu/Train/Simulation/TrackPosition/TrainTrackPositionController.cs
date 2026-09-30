using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Connection;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Core.Simulation;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    [DisallowMultipleComponent]
    public sealed class TrainTrackPositionController : MonoBehaviour, ISimulationController, ITrackOccupancySource
    {
        [SerializeField] private TrackGraphController trackGraphController;
        private readonly TrainTrackPositionContext context = new();
        private TrainTrackConnectionResolver connectionResolver;

        private TrainTrackConnectionResolver ConnectionResolver => connectionResolver ??= TryResolveNextEdge;

        public TrackGraphController TrackGraphController => trackGraphController;
        public TrainTrackPositionContext Context => context;
        public TrainTrackPositionOutput Output => context.Output;

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
            context.Output.Invalidate();
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
            var bogieDistances = new float[definition.CarCount];
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

                bogieDistances[i] = definition.cars[i].bogieCenterDistanceM;
                if (!TrainTrackPathLogic.IsFinite(bogieDistances[i]) || bogieDistances[i] <= 0f)
                {
                    return false;
                }
            }

            TrainTrackSamplingLogic.ConfigureLayout(context, lengths, bogieDistances);
            return true;
        }

        // 固定先頭車の中心を配置し、現在のGraph接続から出力を再計算する。
        public void SetTrackPosition(string edgeId, float distanceOnEdgeM, bool frontFacesAtoB)
        {
            context.State.currentEdgeId = edgeId;
            context.State.distanceOnEdgeM = distanceOnEdgeM;
            context.State.frontFacesAtoB = frontFacesAtoB;
            context.Input.signedDisplacementM = 0f;
            context.Output.Invalidate();

            Calculate(0f);
        }

        // 車両Indexは0始まり。オフセット[m]は編成の固定前方向が正。
        public bool TryGetTrackSample(int carIndex, float offsetFromCarCenterM, out TrainTrackSample sample)
        {
            sample = default;
            return TryGetGraphContext(out var graph) &&
                TrainTrackSamplingLogic.TryGetTrackSample(context, graph, ConnectionResolver,
                    carIndex, offsetFromCarCenterM, out sample);
        }

        public bool TryGetBogies(int carIndex, out TrainTrackSample front, out TrainTrackSample rear) =>
            context.Output.TryGetBogies(carIndex, out front, out rear);

        public bool TryGetOccupiedEdges(List<TrackOccupiedEdgeSpan> destination)
        {
            return context.Output.TryCopyOccupiedEdges(destination);
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
            else
            {
                context.Output.Invalidate();
            }
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
        }
    }
}
