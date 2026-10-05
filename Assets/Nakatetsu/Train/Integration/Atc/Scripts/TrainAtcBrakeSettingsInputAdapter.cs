using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainAtcController))]
    public sealed class TrainAtcBrakeSettingsInputAdapter : MonoBehaviour, ITrainAtcBrakeSettingsInputSource
    {
        private TrainRoot trainRoot;
        private TimsCommunicationController communication;

        public bool TryReadBrakeSettings(TrainAtcBrakeSettingsInput input)
        {
            if (input == null)
            {
                return false;
            }
            input.brakeSubstepCount = 0;
            input.maximumServiceBrakeStep = 0;
            input.brakeTargetDecelerationsMps2.Clear();
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

            var bus = communication.MasterBus;
            if (!bus.TryGetFloatArray(TimsRoot.BrakeTargetDecelerationsMps2Key, out var decelerationsMps2) ||
                !bus.TryGetInt(TimsRoot.BrakeSubstepCountKey, out int brakeSubstepCount) ||
                !bus.TryGetInt(TimsRoot.MaximumServiceBrakeStepKey, out int maximumServiceBrakeStep) ||
                decelerationsMps2.Length == 0 || brakeSubstepCount <= 0 || maximumServiceBrakeStep <= 0 ||
                maximumServiceBrakeStep != (long)(decelerationsMps2.Length - 1) * brakeSubstepCount + 1)
            {
                return false;
            }
            foreach (float value in decelerationsMps2)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    return false;
                }
            }

            // 3項目を確認してからコピーする。未取得・不正な場合は前回値を残さない。
            input.brakeSubstepCount = brakeSubstepCount;
            input.maximumServiceBrakeStep = maximumServiceBrakeStep;
            input.brakeTargetDecelerationsMps2.AddRange(decelerationsMps2);
            return true;
        }
    }
}
