using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainAtcController))]
    public sealed class TrainAtcDoorInputAdapter : MonoBehaviour, ITrainAtcDoorInputSource
    {
        private TrainRoot trainRoot;
        private TimsCommunicationController communication;

        public bool TryReadDoorState(out bool areAllDoorsClosed)
        {
            areAllDoorsClosed = false;
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
            if (trainRoot == null || !trainRoot.isActiveAndEnabled)
            {
                return false;
            }

            if (communication == null)
            {
                TimsCommunicationController foundCommunication = null;
                foreach (var candidate in trainRoot.GetComponentsInChildren<TimsCommunicationController>(true))
                {
                    if (foundCommunication != null)
                    {
                        return false;
                    }
                    foundCommunication = candidate;
                }
                communication = foundCommunication;
            }
            if (communication == null || !communication.isActiveAndEnabled)
            {
                return false;
            }

            // 集約結果が正常な場合だけ全閉状態を採用する。未取得時は前回値を使わない。
            var bus = communication.MasterBus;
            if (!bus.TryGetBool(TimsDoorController.HasValidStateKey, out bool hasValidState) ||
                !hasValidState ||
                !bus.TryGetBool(TimsDoorController.AllClosedKey, out bool allClosed))
            {
                return false;
            }

            areAllDoorsClosed = allClosed;
            return true;
        }
    }
}
