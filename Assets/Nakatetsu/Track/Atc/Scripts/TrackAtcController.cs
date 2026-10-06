using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;
using NewAtc = Nakatetsu.Track.NewAtc;

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

        private readonly NewAtc.TrackAtcContext context = new();

        public NewAtc.TrackAtcContext Context => context;

        public void SetWorldTimeSource(MonoBehaviour source) => worldTimeSource = source;

        public void Calculate(float deltaTimeSeconds)
        {
            context.Graph = atcGraphAsset != null ? atcGraphAsset.Definition : null;
            CollectInput();
            NewAtc.TrackAtcLogic.Calculate(context);
        }

        private void CollectInput()
        {
            NewAtc.TrackAtcInput input = context.Input;
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

                foreach (InterlockingRoute route in interlocking.Routes)
                {
                    // 初期化済み連動の定義にあり、状態がない進路は確認済みの未設定。
                    // 連動を参照できない場合の未登録とは区別し、全定義を毎回写す。
                    var routeInput = new NewAtc.TrackAtcRouteInput();
                    if (interlocking.TryGetRouteState(route.routeId, out var state))
                    {
                        routeInput.IsRouteSet = true;
                        routeInput.ProceedAllowed = state.ProceedAllowed;
                        routeInput.PathEstablished = state.PathEstablished;
                        routeInput.OverrunMode = state.overrunProtectionMode;
                        routeInput.CancelPending = state.CancelPending;
                        routeInput.RouteLocked = state.RouteLocked;
                    }

                    // コンパイル時と同様に、連動装置を跨いでも進路IDは一意とする。
                    input.RoutesById.Add(route.routeId, routeInput);
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
