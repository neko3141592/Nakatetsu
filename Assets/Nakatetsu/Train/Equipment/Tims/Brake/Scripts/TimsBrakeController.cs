using System;
using UnityEngine;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsBrakeController : MonoBehaviour
    {
        public static readonly TimsTagKey TargetAirBrakeForcesNKey =
            new("Brake", "TargetAirBrakeForcesN");
        public static readonly TimsTagKey IsEmergencyKey =
            new("Brake", "IsEmergency");

        [SerializeField] private TimsCommunicationController communicationController;

        private readonly TimsBrakeContext context = new();

        public TimsBrakeContext Context => context;
        public TimsBrakeOutput Output => context.Output;

        private void Awake()
        {
            ResolveCommunicationController();
        }

        public void Configure(TimsCommunicationController controller)
        {
            communicationController = controller;
        }

        public bool CalculateAndPublish()
        {
            if (!ResolveCommunicationController())
            {
                return false;
            }

            TimsBrakeLogic.Calculate(context);
            PublishToMasterBus(communicationController.MasterBus);
            return true;
        }

        public bool TryGetTargetAirBrakeForceN(
            int carIndex,
            out float targetAirBrakeForceN)
        {
            targetAirBrakeForceN = 0f;
            if (!ResolveCommunicationController() || carIndex < 0)
            {
                return false;
            }

            TimsBusState masterBus = communicationController.MasterBus;
            if (!masterBus.TryGetFloatArray(
                TargetAirBrakeForcesNKey,
                out float[] targetAirBrakeForcesN))
            {
                return false;
            }
            if (carIndex >= targetAirBrakeForcesN.Length)
            {
                return false;
            }

            targetAirBrakeForceN = Math.Max(0f, targetAirBrakeForcesN[carIndex]);
            return true;
        }

        private void PublishToMasterBus(TimsBusState masterBus)
        {
            masterBus.SetBool(IsEmergencyKey, context.Output.isEmergency);
            if (!context.Output.hasCommands)
            {
                masterBus.Remove(TargetAirBrakeForcesNKey);
                return;
            }

            var targetAirBrakeForcesN = new float[context.Output.carCommands.Count];
            for (int carIndex = 0; carIndex < context.Output.carCommands.Count; carIndex++)
            {
                TimsBrakeCarCommand command = context.Output.carCommands[carIndex];
                targetAirBrakeForcesN[carIndex] = command != null
                    ? Math.Max(0f, command.targetAirForceN)
                    : 0f;
            }

            masterBus.SetFloatArray(TargetAirBrakeForcesNKey, targetAirBrakeForcesN);
        }

        private bool ResolveCommunicationController()
        {
            if (communicationController == null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }

            return communicationController != null;
        }

        private void OnValidate()
        {
            ResolveCommunicationController();
        }
    }
}
