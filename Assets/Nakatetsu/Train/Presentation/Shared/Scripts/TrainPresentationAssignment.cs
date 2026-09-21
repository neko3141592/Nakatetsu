using UnityEngine;

namespace Nakatetsu.Train.Presentation.Shared
{
    /// <summary>車両のPresentationルートに付け、編成内の所属車両を識別する。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainPresentationAssignment : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int assignedCarIndex = -1;
        [SerializeField, HideInInspector] private TrainPresentationBuilder assignedBy;

        /// <summary>編成先頭を0とする車両インデックス。未割り当ての場合は-1。</summary>
        public int AssignedCarIndex => assignedCarIndex;
        public int DisplayCarNumber => assignedCarIndex >= 0 ? assignedCarIndex + 1 : 0;
        public bool IsAssigned => assignedCarIndex >= 0;

        public void AssignCarIndex(int carIndex)
        {
            assignedCarIndex = Mathf.Max(-1, carIndex);
        }

        internal void AssignGenerated(int carIndex, TrainPresentationBuilder builder)
        {
            AssignCarIndex(carIndex);
            assignedBy = builder;
        }

        internal bool IsGeneratedBy(TrainPresentationBuilder builder)
        {
            return assignedBy == builder;
        }

        private void OnValidate()
        {
            assignedCarIndex = Mathf.Max(-1, assignedCarIndex);
        }
    }
}
