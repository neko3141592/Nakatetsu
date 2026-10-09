using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    [DisallowMultipleComponent]
    public sealed class TrackStationInterlockingController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private TrackStationInterlockingAsset interlockingAsset;
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;
        [SerializeField] private TrackConnectionController trackConnectionController;

        private readonly TrackStationInterlockingContext context = new();

        public TrackStationInterlockingContext Context => context;
        public bool IsInitialized => context.State.isInitialized;
        public IEnumerable<string> RouteIds => context.Output.RoutesById.Keys;

        public bool TryGetRouteStatus(string routeId, out TrackStationInterlockingRouteStatus status) =>
            TrackStationInterlockingLogic.TryGetRouteStatus(context, routeId, out status);

        public bool TryGetCircuitPassage(string routeId, string circuitId,
            out TrackStationInterlockingCircuitPassageState passage) =>
            TrackStationInterlockingLogic.TryGetCircuitPassage(context, routeId, circuitId, out passage);

        private void Awake()
        {
            if (!TryInitialize(out string error))
            {
                Debug.LogError($"Track interlocking initialization failed: {error}", this);
            }
        }

        public bool TryInitialize(out string error)
        {
            CollectInput();
            return TrackStationInterlockingLogic.TryInitialize(context,
                interlockingAsset != null ? interlockingAsset.Definition : null, out error);
        }

        public bool TryRequestRoute(string routeId, out string error)
        {
            CollectInput();
            return TrackStationInterlockingLogic.TryRequestRoute(context, routeId, out error);
        }

        public bool TryCancelRoute(string routeId, out string error)
        {
            CollectInput();
            return TrackStationInterlockingLogic.TryCancelRoute(context, routeId, out error);
        }

        public bool CanRequestTurnoutPosition(string routeId, string connectionId)
        {
            CollectInput();
            return TrackStationInterlockingLogic.CanRequestTurnoutPosition(context, routeId, connectionId);
        }

        public void Calculate(float deltaTimeSeconds)
        {
            CollectInput();
            TrackStationInterlockingLogic.Calculate(context, deltaTimeSeconds);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            var revision = context.Output.StateRevision;
            foreach (TrackStationInterlockingTurnoutCommand command in context.Output.TurnoutCommands)
            {
                // 操作APIで公開値が更新されたら、古い転換要求を送らない。
                if (context.Output.StateRevision != revision)
                {
                    break;
                }

                CollectInput();
                if (!TrackStationInterlockingLogic.CanRequestTurnoutPosition(context,
                    command.RouteId, command.ConnectionId))
                {
                    continue;
                }

                if (!trackConnectionController.TryRequestPosition(command.ConnectionId,
                    command.RequiredPosition, out string error))
                {
                    Debug.LogError($"Switch request failed for route '{command.RouteId}': {error}", this);
                }
            }
        }

        private void CollectInput()
        {
            TrackStationInterlockingInput input = context.Input;
            input.OccupiedByCircuitId.Clear();
            input.ConnectionsById.Clear();
            input.hasCircuitSource = trackCircuitSimulation != null;
            input.hasConnectionSource = trackConnectionController != null && trackConnectionController.IsInitialized;

            if (input.hasCircuitSource)
            {
                foreach (var pair in trackCircuitSimulation.Context.State.OccupiedByCircuitId)
                {
                    input.OccupiedByCircuitId.Add(pair.Key, pair.Value);
                }
            }

            if (!input.hasConnectionSource)
            {
                return;
            }

            IEnumerable<string> connectionIds = IsInitialized
                ? context.Settings.ConnectionIds
                : interlockingAsset != null ? interlockingAsset.Definition.memberConnectionIds : null;
            if (connectionIds == null)
            {
                return;
            }

            foreach (string connectionId in connectionIds)
            {
                if (trackConnectionController.TryGetState(connectionId, out var state))
                {
                    input.ConnectionsById[connectionId] = new TrackStationInterlockingTurnoutInput(
                        state.ActualPosition, state.IsMoving);
                }
            }
        }
    }
}
