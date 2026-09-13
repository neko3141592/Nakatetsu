using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Drive
{
   [CreateAssetMenu(
        fileName = "TrainDriveDefinition",
        menuName = "Nakatetsu/Train/Drive Definition")]
    public sealed class TrainDriveDefinition : ScriptableObject
    {
        [Tooltip("減速比。motor rpm / wheel rpm")]
        [Min(0.01f)]
        public float gearRatio = 7.07f;

        [Tooltip("車輪の有効転動半径 [m]")]
        [Min(0.01f)]
        public float wheelRadiusM = 0.43f;

        [Tooltip("歯車・継手など駆動系全体の機械伝達効率")]
        [Range(0f, 1f)]
        public float transmissionEfficiency = 0.93f;

        private void OnValidate()
        {
            gearRatio = Mathf.Max(0.01f, gearRatio);
            wheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
            transmissionEfficiency = Mathf.Clamp01(transmissionEfficiency);
        }
    }
}