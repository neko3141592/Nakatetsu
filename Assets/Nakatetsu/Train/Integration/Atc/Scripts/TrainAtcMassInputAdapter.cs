using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainAtcController))]
    public sealed class TrainAtcMassInputAdapter : MonoBehaviour, ITrainAtcMassInputSource
    {
        private TrainRoot trainRoot;
        private TimsCommunicationController communication;
        private readonly List<TrainAtcCarInput> carInputs = new();

        public bool TryReadCarInputs(List<TrainAtcCarInput> cars)
        {
            if (cars == null)
            {
                return false;
            }
            cars.Clear();
            carInputs.Clear();
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
            if (trainRoot == null || !trainRoot.isActiveAndEnabled ||
                trainRoot.ConsistDefinition == null || trainRoot.ConsistDefinition.CarCount == 0)
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

            float distanceFromFrontM = 0f;
            var consist = trainRoot.ConsistDefinition;
            for (int carIndex = 0; carIndex < consist.CarCount; carIndex++)
            {
                var definition = consist.cars[carIndex];
                if (definition == null || !IsPositiveFinite(definition.emptyMassKg) ||
                    !IsPositiveFinite(definition.lengthM) ||
                    !communication.TryGetLocalBus(carIndex, out var bus) ||
                    !bus.TryGetFloat(BrakeControlDeviceTimsBusSource.MassKgKey, out float measuredMassKg) ||
                    !IsPositiveFinite(measuredMassKg))
                {
                    return false;
                }

                // TIMSの測定値は空車質量を含む。応荷重分だけを定義の空車質量へ加える。
                float loadMassKg = Mathf.Max(0f, measuredMassKg - definition.emptyMassKg);
                float massKg = definition.emptyMassKg + loadMassKg;
                float centerDistanceM = distanceFromFrontM + definition.lengthM * 0.5f;
                distanceFromFrontM += definition.lengthM;
                if (!IsPositiveFinite(massKg) || !IsPositiveFinite(centerDistanceM) ||
                    !IsPositiveFinite(distanceFromFrontM))
                {
                    return false;
                }

                carInputs.Add(new TrainAtcCarInput
                {
                    massKg = massKg,
                    centerDistanceFromFrontM = centerDistanceM
                });
            }

            cars.AddRange(carInputs);
            return true;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }
    }
}
