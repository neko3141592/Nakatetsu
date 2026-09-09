using UnityEngine;

namespace Nakatetsu.Train.Equipment
{
    [DisallowMultipleComponent]
    public sealed class TrainEquipmentAssignment : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int assignedCarIndex = -1;
        [SerializeField, HideInInspector] private TrainEquipmentBuilder assignedBy;

        /// <summary>編成先頭を0とする車両インデックス。未割り当ての場合は-1。</summary>
        public int AssignedCarIndex => assignedCarIndex;
        public int DisplayCarNumber => assignedCarIndex >= 0 ? assignedCarIndex + 1 : 0;
        public bool IsAssigned => assignedCarIndex >= 0;

        public void AssignCarIndex(int carIndex)
        {
            assignedCarIndex = Mathf.Max(-1, carIndex);
        }

        internal void AssignGenerated(int carIndex, TrainEquipmentBuilder builder)
        {
            assignedCarIndex = Mathf.Max(0, carIndex);
            assignedBy = builder;
        }

        internal bool IsGeneratedBy(TrainEquipmentBuilder builder)
        {
            return assignedBy == builder;
        }

        private void OnValidate()
        {
            assignedCarIndex = Mathf.Max(-1, assignedCarIndex);
        }
    }
}
