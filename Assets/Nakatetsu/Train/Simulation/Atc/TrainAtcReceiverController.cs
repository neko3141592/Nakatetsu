using Nakatetsu.Core.Simulation;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.TrackPosition;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Atc
{
    [DisallowMultipleComponent]
    public sealed class TrainAtcReceiverController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrainTrackPositionController trackPositionController;
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;

        // 車両中心からの距離[m]。編成の固定前方向が正。
        [SerializeField] private float offsetFromCarCenterM;
        private readonly TrainAtcReceiverContext context = new();

        public TrainAtcReceiverContext Context => context;
        public float OffsetFromCarCenterM => offsetFromCarCenterM;

        // 車上ATCの初期設定に使う取り付け位置のサンプル。ATC Edgeの決定は行わない。
        public bool TryGetTrackSample(out TrainTrackSample sample)
        {
            sample = default;
            if (!isActiveAndEnabled || trackPositionController == null ||
                !trackPositionController.isActiveAndEnabled)
            {
                return false;
            }

            var assignment = GetComponentInParent<TrainSimulationAssignment>(true);
            if (assignment == null || !assignment.IsAssigned)
            {
                return false;
            }

            return trackPositionController.TryGetTrackSample(
                assignment.AssignedCarIndex, offsetFromCarCenterM, out sample);
        }

        public void SetTrackPositionController(TrainTrackPositionController controller)
        {
            trackPositionController = controller;
            ClearPosition();
        }

        // 同じステップで計算した線路Edgeと、Node Aからの実距離を返す。
        public bool TryGetEdgePosition(out string edgeId, out float distanceOnEdgeM)
        {
            edgeId = null;
            distanceOnEdgeM = 0f;
            if (!isActiveAndEnabled)
            {
                return false;
            }

            return TrainAtcReceiverLogic.TryGetEdgePosition(context, out edgeId, out distanceOnEdgeM);
        }

        public void Calculate(float deltaTimeSeconds)
        {
            CollectInput();
            if (trackPositionController != null && trackPositionController.TryGetGraphContext(out var graph)
                && trackCircuitSimulation != null && trackCircuitSimulation.isActiveAndEnabled)
            {
                TrainAtcReceiverLogic.Calculate(context, graph, trackCircuitSimulation.Context.State);
            }
            else
            {
                TrainAtcReceiverLogic.Calculate(context);
            }
        }

        public void SetTrackCircuitSimulation(TrackCircuitSimulationController controller)
        {
            trackCircuitSimulation = controller;
            ClearPosition();
        }

        public bool TryGetTelegram(out TrackCircuitAtcTelegram telegram)
        {
            telegram = null;
            if (!isActiveAndEnabled) return false;
            return TrainAtcReceiverLogic.TryGetTelegram(context, out telegram);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
        }

        private void CollectInput()
        {
            context.Input.hasEdgePosition = false;
            context.Input.edgeId = null;
            context.Input.distanceOnEdgeM = 0f;
            if (!TryGetTrackSample(out var sample))
            {
                return;
            }

            context.Input.hasEdgePosition = true;
            context.Input.edgeId = sample.EdgeId;
            context.Input.distanceOnEdgeM = sample.DistanceOnEdgeM;
        }

        private void ClearPosition()
        {
            context.Input.hasEdgePosition = false;
            context.Input.edgeId = null;
            context.Input.distanceOnEdgeM = 0f;
            TrainAtcReceiverLogic.Calculate(context);
        }

        private void OnDisable()
        {
            ClearPosition();
        }
    }
}
