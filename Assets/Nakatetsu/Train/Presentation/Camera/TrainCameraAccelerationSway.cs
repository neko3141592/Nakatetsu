using Nakatetsu.Train.Presentation.Shared;
using Nakatetsu.Train.Simulation.Physics;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Camera
{
    // 車体姿勢の更新後、Gameカメラがアンカーを読む前に視点の慣性を反映する。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainCameraAnchor))]
    [DefaultExecutionOrder(50)]
    public sealed class TrainCameraAccelerationSway : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float backwardOffsetMPerMps2 = 0.025f;
        [SerializeField, Min(0f)] private float upwardPitchDegreesPerMps2 = 1f;
        [SerializeField, Min(0f)] private float maxAccelerationMps2 = 2f;
        [SerializeField, Min(0.01f)] private float smoothTimeSeconds = 0.2f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private TrainRoot trainRoot;
        private TrainPhysicsController physics;
        private TrainPresentationAssignment assignment;
        private float longitudinalOffsetM;
        private float pitchDegrees;
        private float offsetVelocityMps;
        private float pitchVelocityDegreesPerSecond;

        private void OnEnable()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            longitudinalOffsetM = 0f;
            pitchDegrees = 0f;
            offsetVelocityMps = 0f;
            pitchVelocityDegreesPerSecond = 0f;
        }

        private void LateUpdate()
        {
            float accelerationMps2 = GetCabForwardAccelerationMps2();
            float limitedAccelerationMps2 = Mathf.Clamp(
                accelerationMps2, -maxAccelerationMps2, maxAccelerationMps2);
            float deltaTimeSeconds = Time.deltaTime;

            longitudinalOffsetM = Mathf.SmoothDamp(
                longitudinalOffsetM,
                -limitedAccelerationMps2 * backwardOffsetMPerMps2,
                ref offsetVelocityMps,
                smoothTimeSeconds,
                Mathf.Infinity,
                deltaTimeSeconds);
            pitchDegrees = Mathf.SmoothDampAngle(
                pitchDegrees,
                -limitedAccelerationMps2 * upwardPitchDegreesPerMps2,
                ref pitchVelocityDegreesPerSecond,
                smoothTimeSeconds,
                Mathf.Infinity,
                deltaTimeSeconds);

            transform.localPosition = baseLocalPosition + Vector3.forward * longitudinalOffsetM;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private float GetCabForwardAccelerationMps2()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            if (trainRoot == null)
            {
                return 0f;
            }

            if (physics == null)
            {
                physics = trainRoot.GetComponentInChildren<TrainPhysicsController>(true);
            }

            if (physics == null)
            {
                return 0f;
            }

            if (assignment == null)
            {
                assignment = GetComponentInParent<TrainPresentationAssignment>(true);
            }

            float accelerationMps2 = physics.Context.Output.signedAcceleration;
            if (float.IsNaN(accelerationMps2) || float.IsInfinity(accelerationMps2))
            {
                return 0f;
            }

            // Tc2 Variantは後ろ向きの運転台なので、車両前方基準の加速度を反転する。
            bool rearCab = assignment != null && assignment.IsAssigned &&
                trainRoot.ConsistDefinition != null && trainRoot.ConsistDefinition.CarCount > 1 &&
                assignment.AssignedCarIndex == trainRoot.ConsistDefinition.CarCount - 1;
            return rearCab ? -accelerationMps2 : accelerationMps2;
        }

        private void OnDisable()
        {
            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
        }
    }
}
