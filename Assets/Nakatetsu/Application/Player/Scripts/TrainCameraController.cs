using System.Collections.Generic;
using Nakatetsu.Train;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Presentation.Camera;
using Nakatetsu.Train.Presentation.Shared;
using UnityEngine;

namespace Nakatetsu.Application.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class TrainCameraController : MonoBehaviour
    {
        [SerializeField] private TrainFocusController focusController;

        private readonly List<TrainCameraAnchor> anchors = new List<TrainCameraAnchor>();

        private void LateUpdate()
        {
            if (!TryGetAnchor(out TrainCameraAnchor anchor))
            {
                return;
            }

            // 車体のLateUpdateが終わってから、目線の位置と向きを反映する。
            transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
        }

        private bool TryGetAnchor(out TrainCameraAnchor anchor)
        {
            anchor = null;
            if (focusController == null || !focusController.isActiveAndEnabled)
            {
                return false;
            }

            var train = focusController.FocusedTrain;
            if (train == null || !train.isActiveAndEnabled || train.Status == null)
            {
                return false;
            }

            if (!train.Status.TryGetActiveCab(out ActivatedCabPosition cabPosition))
            {
                return false;
            }

            var trainRoot = train.GetComponentInParent<TrainRoot>();
            if (trainRoot == null || trainRoot.ConsistDefinition == null || trainRoot.ConsistDefinition.CarCount == 0)
            {
                return false;
            }

            int carIndex;
            switch (cabPosition)
            {
                case ActivatedCabPosition.Front:
                    carIndex = 0;
                    break;
                case ActivatedCabPosition.Rear:
                    carIndex = trainRoot.ConsistDefinition.CarCount - 1;
                    break;
                default:
                    return false;
            }

            trainRoot.GetComponentsInChildren<TrainCameraAnchor>(false, anchors);
            foreach (TrainCameraAnchor candidate in anchors)
            {
                if (!candidate.isActiveAndEnabled || candidate.GetComponentInParent<TrainRoot>() != trainRoot)
                {
                    continue;
                }

                var assignment = candidate.GetComponentInParent<TrainPresentationAssignment>();
                if (assignment == null || !assignment.IsAssigned || assignment.AssignedCarIndex != carIndex)
                {
                    continue;
                }

                // 同じ車両に複数ある場合は、目線を勝手に選ばない。
                if (anchor != null)
                {
                    anchor = null;
                    return false;
                }

                anchor = candidate;
            }

            return anchor != null;
        }
    }
}
