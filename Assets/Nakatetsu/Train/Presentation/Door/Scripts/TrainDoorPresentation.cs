using Nakatetsu.Train.Presentation.Shared;
using Nakatetsu.Train.Simulation.Door;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Door
{
    /// <summary>1か所のドアの戸を、Simulationの開度に合わせて移動する。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Train/Presentation/Door")]
    public sealed class TrainDoorPresentation : MonoBehaviour
    {
        [Header("Simulation")]
        [Tooltip("未指定の場合、Start時に親のPresentationAssignmentと同じ編成・車両Indexから接続します。")]
        [SerializeField] private TrainDoorSimulation simulation;
        [Tooltip("編成の基準前方に対する左側。右側はオフ。")]
        [SerializeField] private bool leftSide = true;
        [Tooltip("その側の前方から数えたドア番号。0が最前部です。")]
        [SerializeField, Min(0)] private int doorIndex;

        [Header("Door Leaves")]
        [Tooltip("再生開始時のlocalPositionを閉位置として記録します。")]
        [SerializeField] private Transform firstLeaf;
        [Tooltip("全開時の移動量。戸の親Transformのローカル座標系で指定します。")]
        [SerializeField] private Vector3 firstLeafOpenOffset;
        [Tooltip("両開きのもう1枚の戸。片開きの場合は未指定で構いません。")]
        [SerializeField] private Transform secondLeaf;
        [Tooltip("全開時の移動量。戸の親Transformのローカル座標系で指定します。")]
        [SerializeField] private Vector3 secondLeafOpenOffset;

        private Vector3 firstLeafClosedPosition;
        private Vector3 secondLeafClosedPosition;

        private void Awake()
        {
            if (firstLeaf != null) firstLeafClosedPosition = firstLeaf.localPosition;
            if (secondLeaf != null) secondLeafClosedPosition = secondLeaf.localPosition;
        }

        private void Start()
        {
            if (simulation == null && !BindSimulationFromAssignment())
            {
                Debug.LogWarning(
                    $"{nameof(TrainDoorPresentation)}: Simulationを指定するか、同じ編成・車両IndexのAssignmentとSimulationを1組設定してください。",
                    this);
            }
        }

        /// <summary>生成後の接続やSimulationの差し替えに使用する。</summary>
        public void SetSimulation(TrainDoorSimulation source)
        {
            simulation = source;
        }

        /// <summary>同じTrainRoot内で、親のPresentationAssignmentと一致するSimulationを接続する。</summary>
        public bool BindSimulationFromAssignment()
        {
            simulation = null;
            TrainPresentationAssignment presentationAssignment = GetComponentInParent<TrainPresentationAssignment>(true);
            TrainRoot trainRoot = GetComponentInParent<TrainRoot>(true);
            if (presentationAssignment == null || !presentationAssignment.IsAssigned || trainRoot == null ||
                presentationAssignment.GetComponentInParent<TrainRoot>(true) != trainRoot)
                return false;

            TrainDoorSimulation match = null;
            foreach (TrainDoorSimulation candidate in trainRoot.GetComponentsInChildren<TrainDoorSimulation>(true))
            {
                if (candidate.GetComponentInParent<TrainRoot>(true) != trainRoot) continue;
                TrainSimulationAssignment assignment = candidate.GetComponentInParent<TrainSimulationAssignment>(true);
                if (assignment == null || !assignment.IsAssigned ||
                    assignment.GetComponentInParent<TrainRoot>(true) != trainRoot ||
                    assignment.AssignedCarIndex != presentationAssignment.AssignedCarIndex) continue;

                // 同じ車両に複数ある場合は、どれかを勝手に選ばない。
                if (match != null) return false;
                match = candidate;
            }

            simulation = match;
            return simulation != null;
        }

        private void LateUpdate()
        {
            if (simulation == null ||
                !simulation.TryGetDoor(leftSide, doorIndex, out float openingRatio, out _, out _)) return;

            // 開閉時間はSimulationが管理する。無効状態では最後の表示位置を保持する。
            if (firstLeaf != null)
                firstLeaf.localPosition = firstLeafClosedPosition + firstLeafOpenOffset * openingRatio;
            if (secondLeaf != null)
                secondLeaf.localPosition = secondLeafClosedPosition + secondLeafOpenOffset * openingRatio;
        }

        private void OnValidate()
        {
            doorIndex = Mathf.Max(0, doorIndex);
        }
    }
}
