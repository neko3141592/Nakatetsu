using UnityEngine;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    [DisallowMultipleComponent]
    public sealed class TrainSimulationAssignment : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int assignedCarIndex = -1;
        [SerializeField, HideInInspector] private TrainSimulationBuilder assignedBy;

        public int AssignedCarIndex => assignedCarIndex;
        public int DisplayCarNumber => assignedCarIndex >= 0 ? assignedCarIndex + 1 : 0;
        public bool IsAssigned => assignedCarIndex >= 0;

        public void AssignCarIndex(int carIndex)
        {
            assignedCarIndex = Mathf.Max(-1, carIndex);
        }

        internal void AssignGenerated(int carIndex, TrainSimulationBuilder builder)
        {
            assignedCarIndex = Mathf.Max(0, carIndex);
            assignedBy = builder;
        }

        internal bool IsGeneratedBy(TrainSimulationBuilder builder)
        {
            return assignedBy == builder;
        }

        private void OnValidate()
        {
            assignedCarIndex = Mathf.Max(-1, assignedCarIndex);
        }
    }
}
