using Nakatetsu.Core.Simulation;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Atc
{
    [DisallowMultipleComponent]
    public sealed class TrackAtcController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrackAtcGraphAsset atcGraphAsset;
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;
        [SerializeField] private TrackConnectionController trackConnectionController;
        [SerializeField] private MonoBehaviour worldTimeSource;
        [SerializeField] private TrackInterlockingManagementController interlockingManagement;

        private readonly TrackAtcContext context = new();

        public TrackAtcContext Context => context;

        public void SetWorldTimeSource(MonoBehaviour source) => worldTimeSource = source;

        public void Calculate(float deltaTimeSeconds)
        {
            context.Graph = atcGraphAsset != null ? atcGraphAsset.Definition : null;
            CollectInput();
            TrackAtcLogic.Calculate(context);
        }

        private void CollectInput()
        {
            TrackAtcInput input = context.Input;
            input.simulationTimeSeconds = worldTimeSource != null && worldTimeSource is IWorldTimeSource clock
                ? clock.WorldTimeSeconds
                : double.NaN;
            input.OccupiedByCircuitId.Clear();
            input.RoutesById.Clear();
            input.PhysicalPathAvailableByAtcEdgeId.Clear();

            if (trackCircuitSimulation != null)
            {
                foreach (var pair in trackCircuitSimulation.Context.State.OccupiedByCircuitId)
                {
                    input.OccupiedByCircuitId.Add(pair.Key, pair.Value);
                }
            }

            CollectPhysicalPathAvailability(input);

            if (interlockingManagement == null || !interlockingManagement.isActiveAndEnabled ||
                !interlockingManagement.IsInitialized)
            {
                return;
            }

            foreach (var station in interlockingManagement.Context.Output.StationsById.Values)
            {
                if (station == null || !station.IsInitialized || !station.HasOutput)
                {
                    continue;
                }

                foreach (var pair in station.RoutesById)
                {
                    var status = pair.Value;
                    if (status == null || !status.IsAvailable)
                    {
                        continue;
                    }

                    // 確認済みの未設定も登録し、状態取得不能の未登録と区別する。
                    var routeInput = new TrackAtcRouteInput
                    {
                        IsRouteSet = status.IsRouteSet,
                        ProceedAllowed = status.ProceedAllowed,
                        PathEstablished = status.PathEstablished,
                        OverrunMode = status.OverrunMode,
                        CancelPending = status.CancelPending,
                        RouteLocked = status.RouteLocked
                    };

                    // コンパイル時と同様に、連動装置を跨いでも進路IDは一意とする。
                    input.RoutesById.Add(pair.Key, routeInput);
                }
            }
        }

        private void CollectPhysicalPathAvailability(TrackAtcInput input)
        {
            if (trackConnectionController == null || !trackConnectionController.IsInitialized ||
                context.Graph?.atcEdge == null)
            {
                return;
            }

            var graphController = trackConnectionController.GetComponent<TrackGraphController>();
            var trackGraph = graphController != null ? graphController.Context : null;
            if (trackGraph == null || !trackGraph.IsInitialized)
            {
                return;
            }

            foreach (var edge in context.Graph.atcEdge)
            {
                if (edge?.physicalSpans == null || edge.physicalSpans.Count < 2)
                {
                    continue;
                }

                bool connected = true;
                for (int i = 1; i < edge.physicalSpans.Count; i++)
                {
                    var previous = edge.physicalSpans[i - 1];
                    var next = edge.physicalSpans[i];
                    if (previous == null || next == null ||
                        !trackGraph.TryGetEdge(previous.trackEdgeId, out var previousEdge) ||
                        !trackGraph.TryGetEdge(next.trackEdgeId, out var nextEdge))
                    {
                        connected = false;
                        break;
                    }

                    if (previous.trackEdgeId == next.trackEdgeId)
                    {
                        connected = previous.endDistanceOnEdgeM == next.startDistanceOnEdgeM;
                    }
                    else
                    {
                        string previousExit = previous.endDistanceOnEdgeM > previous.startDistanceOnEdgeM
                            ? previousEdge.nodeBId : previousEdge.nodeAId;
                        string nextEntry = next.endDistanceOnEdgeM > next.startDistanceOnEdgeM
                            ? nextEdge.nodeAId : nextEdge.nodeBId;
                        connected = previousExit == nextEntry &&
                            trackConnectionController.TryResolveNextEdge(previousExit, previous.trackEdgeId,
                                out string connectedEdgeId, out _) && connectedEdgeId == next.trackEdgeId;
                    }

                    if (!connected)
                    {
                        break;
                    }
                }

                input.PhysicalPathAvailableByAtcEdgeId[edge.atcEdgeId] = connected;
            }
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            if (trackCircuitSimulation != null)
            {
                trackCircuitSimulation.SetAtcTelegrams(context.Output.telegrams);
            }
        }

        private void OnDisable()
        {
            if (trackCircuitSimulation != null)
            {
                trackCircuitSimulation.SetAtcTelegrams(null);
            }
        }
    }
}
