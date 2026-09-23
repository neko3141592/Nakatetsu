using Nakatetsu.Train.Presentation.Shared;
using Nakatetsu.Train.Simulation.TrackPosition;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Bogie
{
    [DisallowMultipleComponent]
    public sealed class TrainBogiePresentation : MonoBehaviour
    {
        private TrainPresentationAssignment assignment;
        private TrainRoot trainRoot;
        private TrainTrackPositionController trackPosition;
        private Quaternion modelRotation;

        private void Start()
        {
            modelRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            if (assignment == null)
            {
                assignment = GetComponent<TrainPresentationAssignment>();
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            if (assignment == null || !assignment.IsAssigned || trainRoot == null || trainRoot.ConsistDefinition == null)
            {
                return;
            }

            int carIndex = assignment.AssignedCarIndex;
            if (carIndex >= trainRoot.ConsistDefinition.CarCount)
            {
                return;
            }

            var carDefinition = trainRoot.ConsistDefinition.cars[carIndex];
            if (carDefinition == null || carDefinition.bogieCenterDistanceM <= 0f)
            {
                return;
            }

            if (trackPosition == null)
            {
                trackPosition = trainRoot.GetComponentInChildren<TrainTrackPositionController>(true);
            }

            if (trackPosition == null)
            {
                return;
            }

            float halfDistanceM = carDefinition.bogieCenterDistanceM * 0.5f;
            if (!trackPosition.TryGetTrackSample(carIndex, halfDistanceM, out var front) ||
                !trackPosition.TryGetTrackSample(carIndex, -halfDistanceM, out var rear))
            {
                return;
            }

            Vector3 direction = front.Position - rear.Position;
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            // 前後台車の中点と結線方向から車体の姿勢を決める。
            Vector3 center = (front.Position + rear.Position) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.SetPositionAndRotation(center, rotation * modelRotation);
        }
    }
}
