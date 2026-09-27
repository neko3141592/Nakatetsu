using System.Collections.Generic;
using Nakatetsu.Track.Graph.Circuit;

namespace Nakatetsu.Track.Simulation.Circuit
{
    public sealed class TrackCircuitSimulationState
    {
        private readonly Dictionary<string, bool> occupiedByCircuitId = new();
        private readonly IReadOnlyDictionary<string, bool> occupiedView;

        public TrackCircuitSimulationState()
        {
            occupiedView = new System.Collections.ObjectModel.ReadOnlyDictionary<string, bool>(occupiedByCircuitId);
        }

        public IReadOnlyDictionary<string, bool> OccupiedByCircuitId => occupiedView;

        // 未登録IDも安全側の占有として扱う。
        public bool IsOccupied(string circuitId) =>
            circuitId == null || !occupiedByCircuitId.TryGetValue(circuitId, out bool occupied) || occupied;

        internal void SetAllOccupied(IEnumerable<TrackCircuitDefinition> circuits)
        {
            occupiedByCircuitId.Clear();
            if (circuits == null)
            {
                return;
            }

            foreach (TrackCircuitDefinition circuit in circuits)
            {
                occupiedByCircuitId[circuit.circuitId] = true;
            }
        }

        internal void SetOccupied(string circuitId, bool occupied) =>
            occupiedByCircuitId[circuitId] = occupied;
    }

    public sealed class TrackCircuitSimulationContext
    {
        public TrackCircuitSimulationState State { get; } = new();
    }
}
