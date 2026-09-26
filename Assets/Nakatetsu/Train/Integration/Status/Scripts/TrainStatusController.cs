using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Simulation.Physics;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    public sealed class TrainStatusController : MonoBehaviour
    {
        private TrainRoot trainRoot;
        private TrainPhysicsController physicsController;
        private TrainCabResolver resolver;

        internal void Initialize(TrainCabResolver cabResolver)
        {
            resolver = cabResolver;
        }

        private void Awake()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
        }

        /// <summary>物理計算から速度の大きさをm/sで取得する。ゲーム側の表示用。</summary>
        public bool TryGetSpeedMps(out float speedMps)
        {
            speedMps = 0f;
            if (!isActiveAndEnabled || trainRoot == null || !trainRoot.isActiveAndEnabled)
            {
                return false;
            }

            if (physicsController == null)
            {
                physicsController = trainRoot.GetComponentInChildren<TrainPhysicsController>(true);
            }

            if (physicsController == null || !physicsController.isActiveAndEnabled ||
                physicsController.GetComponentInParent<TrainRoot>(true) != trainRoot)
            {
                return false;
            }

            float signedVelocityMps = physicsController.Context.Output.signedVelocityMps;
            if (float.IsNaN(signedVelocityMps) || float.IsInfinity(signedVelocityMps))
            {
                return false;
            }

            speedMps = Mathf.Abs(signedVelocityMps);
            return true;
        }

        /// <summary>取得元が利用できない場合はfalse。有効運転台なしはtrueかつNone。</summary>
        public bool TryGetActiveCab(out ActivatedCabPosition position)
        {
            position = ActivatedCabPosition.None;
            if (!isActiveAndEnabled || resolver == null)
            {
                return false;
            }

            return resolver.TryGetActiveCab(out position);
        }

        /// <summary>
        /// 有効運転台のマスコンから現在の操作位置を読み取る。
        /// 有効運転台・編成定義・対応する機器が利用できない場合はfalseを返す。
        /// </summary>
        public bool TryGetActiveCabControls(out TrainCabControls controls)
        {
            controls = default;
            if (!isActiveAndEnabled || resolver == null)
            {
                return false;
            }

            if (!resolver.TryGetActiveMaster(out MasterController activeMaster, out ActivatedCabPosition cabPosition))
            {
                return false;
            }

            controls = new TrainCabControls(
                cabPosition,
                activeMaster.PowerPosition,
                activeMaster.BrakePosition,
                activeMaster.IsEmergencyBrake,
                activeMaster.ReverserPosition);
            return true;
        }


        public bool TryGetTrainId(out string trainId)
        {
            trainId = null;
            if (trainRoot == null || string.IsNullOrWhiteSpace(trainRoot.TrainId))
            {
                return false;
            }

            trainId = trainRoot.TrainId;
            return true;
        }
    }
}
