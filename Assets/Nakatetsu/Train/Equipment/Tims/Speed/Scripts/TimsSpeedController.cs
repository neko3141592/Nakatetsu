using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Speed
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsSpeedController : MonoBehaviour
    {
        public static readonly TimsTagKey SpeedMpsKey = new("Train", "SpeedMps");
        public static readonly TimsTagKey SpeedKmhKey = new("Train", "SpeedKmh");
        public static readonly TimsTagKey HasValidSpeedKey = new("Train", "HasValidSpeed");

        private TimsCommunicationController communication;
        private TrainRoot trainRoot;

        public bool CalculateAndPublish()
        {
            ResolveReferences();
            if (communication == null)
            {
                return false;
            }

            if (isActiveAndEnabled && trainRoot != null && trainRoot.ConsistDefinition != null)
            {
                // 初期方式：車両順で最初の有効値。平均・多数決・運転台選択は行わない。
                for (int i = 0; i < trainRoot.ConsistDefinition.CarCount; i++)
                {
                    if (!communication.TryGetLocalBus(i, out TimsBusState localBus) ||
                        !localBus.TryGetFloat(SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, out float speedMps) ||
                        float.IsNaN(speedMps) || float.IsInfinity(speedMps) || speedMps < 0f)
                    {
                        continue;
                    }

                    float speedKmh = speedMps * 3.6f;
                    if (float.IsInfinity(speedKmh)) continue;

                    communication.MasterBus.SetFloat(SpeedMpsKey, speedMps);
                    communication.MasterBus.SetFloat(SpeedKmhKey, speedKmh);
                    communication.MasterBus.SetBool(HasValidSpeedKey, true);
                    return true;
                }
            }

            ClearPublishedValue();
            return false;
        }

        private void ResolveReferences()
        {
            if (communication == null) communication = GetComponent<TimsCommunicationController>();
            if (trainRoot == null) trainRoot = GetComponentInParent<TrainRoot>(true);
        }

        private void ClearPublishedValue()
        {
            if (communication == null) return;
            communication.MasterBus.Remove(SpeedMpsKey);
            communication.MasterBus.Remove(SpeedKmhKey);
            communication.MasterBus.SetBool(HasValidSpeedKey, false);
        }

        private void OnDisable()
        {
            ResolveReferences();
            ClearPublishedValue();
        }
    }
}
