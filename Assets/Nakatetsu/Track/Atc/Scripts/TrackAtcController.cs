using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Interlocking;
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
        [SerializeField] private List<TrackInterlockingController> interlockings = new();

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

            if (trackCircuitSimulation != null)
            {
                foreach (var pair in trackCircuitSimulation.Context.State.OccupiedByCircuitId)
                {
                    input.OccupiedByCircuitId.Add(pair.Key, pair.Value);
                }
            }

            foreach (TrackInterlockingController interlocking in interlockings)
            {
                if (interlocking == null || !interlocking.IsInitialized)
                {
                    continue;
                }

                foreach (string routeId in interlocking.RouteIds)
                {
                    if (!interlocking.TryGetRouteStatus(routeId, out var status))
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
                    input.RoutesById.Add(routeId, routeInput);
                }
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
