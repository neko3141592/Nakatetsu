using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    [DisallowMultipleComponent]
    public sealed class TrackInterlockingController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrackInterlockingAsset interlockingAsset;
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;
        [SerializeField] private TrackConnectionController trackConnectionController;

        private readonly TrackInterlockingContext context = new();

        public TrackInterlockingContext Context => context;
        public bool IsInitialized => context.IsInitialized;
        public IEnumerable<InterlockingRoute> Routes => context.RoutesById.Values;

        public bool TryGetRouteState(string routeId, out TrackInterlockingRouteState state) =>
            context.RouteStatesById.TryGetValue(routeId, out state);

        public bool TryGetCircuitPassage(string routeId, string circuitId,
            out TrackInterlockingCircuitPassageState passage)
        {
            passage = default;
            return context.RouteStatesById.TryGetValue(routeId, out var state) &&
                state.CircuitPassageById.TryGetValue(circuitId, out passage);
        }

        private void Awake()
        {
            if (!TryInitialize(out string error))
                Debug.LogError($"Track interlocking initialization failed: {error}", this);
        }

        public bool TryInitialize(out string error)
        {
            if (trackCircuitSimulation == null || trackConnectionController == null)
            {
                error = "Track circuit and connection controllers are required.";
                return false;
            }

            return TrackInterlockingLogic.TryInitialize(context,
                interlockingAsset != null ? interlockingAsset.Definition : null, out error);
        }

        public bool TryRequestRoute(string routeId, out string error)
        {
            if (trackCircuitSimulation == null || trackConnectionController == null)
            {
                error = "Track circuit and connection controllers are required.";
                return false;
            }

            return TrackInterlockingLogic.TryRequestRoute(context,
                trackCircuitSimulation.Context.State,
                trackConnectionController.Context,
                routeId,
                out error);
        }

        public bool TryCancelRoute(string routeId, out string error)
        {
            if (trackCircuitSimulation == null)
            {
                error = "Track circuit controller is required.";
                return false;
            }

            return TrackInterlockingLogic.TryCancelRoute(context,
                trackCircuitSimulation.Context.State,
                routeId,
                out error);
        }

        public bool CanRequestTurnoutPosition(string routeId, string connectionId) =>
            trackCircuitSimulation != null &&
            TrackInterlockingLogic.CanRequestTurnoutPosition(context,
                trackCircuitSimulation.Context.State, routeId, connectionId);

        public void Calculate(float deltaTimeSeconds)
        {
            if (!IsInitialized) return;

            TrackInterlockingLogic.Calculate(context,
                trackCircuitSimulation.Context.State,
                trackConnectionController.Context,
                deltaTimeSeconds);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            if (!IsInitialized) return;

            foreach (var pair in context.RouteStatesById)
            {
                InterlockingRoute route = context.RoutesById[pair.Key];
                if (pair.Value.ProceedAllowed)
                    continue;

                // 本進路に加えて、保持中の過走防護用転轍機にも転換を要求する。
                foreach (TurnoutRequirement turnout in TrackInterlockingLogic.GetRequiredTurnouts(route, pair.Value))
                {
                    if (turnout == null || trackConnectionController == null ||
                        !trackConnectionController.TryGetState(turnout.connectionId, out var switchState) ||
                        switchState.IsMoving || switchState.ActualPosition == turnout.requiredPosition)
                        continue;

                    if (!CanRequestTurnoutPosition(pair.Key, turnout.connectionId))
                        continue;

                    if (!trackConnectionController.TryRequestPosition(
                            turnout.connectionId, turnout.requiredPosition, out string error))
                        Debug.LogError($"Switch request failed for route '{pair.Key}': {error}", this);
                }
            }
        }
    }
}
