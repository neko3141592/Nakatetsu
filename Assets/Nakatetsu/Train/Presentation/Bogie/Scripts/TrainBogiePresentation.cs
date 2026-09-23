using Nakatetsu.Train.Simulation.TrackPosition;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Bogie
{
    [DisallowMultipleComponent]
    public sealed class TrainBogiePresentation : MonoBehaviour
    {
        [SerializeField] private TrainTrackPositionController trackPosition;
        [SerializeField] private int carIndex = -1;
        [SerializeField] private float bogieCenterDistanceM;

        private Quaternion modelRotation = Quaternion.identity;

        public void Configure(TrainTrackPositionController source, int index,
            float centerDistanceM, Quaternion rotation)
        {
            trackPosition = source;
            carIndex = index;
            bogieCenterDistanceM = centerDistanceM;
            modelRotation = rotation;
        }

        private void LateUpdate()
        {
            if (trackPosition == null || carIndex < 0 || bogieCenterDistanceM <= 0f)
            {
                return;
            }

            float halfDistanceM = bogieCenterDistanceM * 0.5f;
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
