using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
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
        [SerializeField] private List<TrackInterlockingController> interlockings = new();

        private readonly TrackAtcContext context = new();

        public TrackAtcContext Context => context;

        public void Calculate(float deltaTimeSeconds)
        {
            context.Graph = atcGraphAsset != null ? atcGraphAsset.Definition : null;
            CollectInput();
            context.Output.NextEdgeById.Clear();
            TrackAtcLogic.Calculate(context, deltaTimeSeconds);
        }

        private void CollectInput()
        {
            TrackAtcInput input = context.Input;
            input.OccupiedByCircuitId.Clear();
            input.RoutesById.Clear();
            input.TurnoutsById.Clear();

            if (trackCircuitSimulation != null)
            {
                foreach (var pair in trackCircuitSimulation.Context.State.OccupiedByCircuitId)
                    input.OccupiedByCircuitId.Add(pair.Key, pair.Value);
            }

            foreach (TrackInterlockingController interlocking in interlockings)
            {
                if (interlocking == null || !interlocking.IsInitialized)
                    continue;

                foreach (InterlockingRoute route in interlocking.Routes)
                {
                    if (!interlocking.TryGetRouteState(route.routeId, out var state))
                        continue;

                    var routeInput = new TrackAtcRouteInput
                    {
                        ProceedAllowed = state.ProceedAllowed,
                        PathEstablished = state.PathEstablished,
                        OverrunMode = state.overrunProtectionMode,
                        CancelPending = state.CancelPending,
                        RouteLocked = state.RouteLocked
                    };

                    foreach (TurnoutRequirement turnout in route.requiredTurnouts)
                    {
                        routeInput.RequiredTurnoutsById.Add(turnout.connectionId, turnout.requiredPosition);
                        if (trackConnectionController != null &&
                            trackConnectionController.TryGetState(turnout.connectionId, out var turnoutState))
                        {
                            input.TurnoutsById[turnout.connectionId] = turnoutState;
                        }
                    }

                    // コンパイル時と同様に、連動装置を跨いでも進路IDは一意とする。
                    input.RoutesById.Add(route.routeId, routeInput);
                }
            }
        }

        public void ApplyOutput(float deltaTimeSeconds) { }
    }
}
