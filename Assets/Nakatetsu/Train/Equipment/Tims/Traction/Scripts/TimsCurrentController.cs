using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    /// <summary>編成内の代表VVVF 1台の実電流を表示用MasterBusへ公開する。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsCurrentController : MonoBehaviour
    {
        public static readonly TimsTagKey SignedMotorCurrentAKey = new("Train", "SignedMotorCurrentA");
        public static readonly TimsTagKey CurrentSourceCarIndexKey = new("Train", "CurrentSourceCarIndex");

        private TimsCommunicationController communication;
        private TrainRoot trainRoot;

        public bool CalculateAndPublish()
        {
            ResolveReferences();
            if (communication == null) return false;
            if (isActiveAndEnabled && trainRoot != null && trainRoot.ConsistDefinition != null)
            {
                // 車両順で最初の有効なVVVFを選ぶ。0Aも有効とし、合算や最大値選択はしない。
                for (int i = 0; i < trainRoot.ConsistDefinition.CarCount; i++)
                {
                    if (!communication.TryGetLocalBus(i, out TimsBusState localBus) ||
                        !localBus.TryGetFloat(TimsTractionBusSource.SignedMotorCurrentAKey, out float currentA) ||
                        float.IsNaN(currentA) || float.IsInfinity(currentA)) continue;

                    communication.MasterBus.SetFloat(SignedMotorCurrentAKey, currentA);
                    communication.MasterBus.SetInt(CurrentSourceCarIndexKey, i);
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
            communication.MasterBus.Remove(SignedMotorCurrentAKey);
            communication.MasterBus.Remove(CurrentSourceCarIndexKey);
        }

        private void OnDisable()
        {
            ResolveReferences();
            ClearPublishedValue();
        }
    }
}
