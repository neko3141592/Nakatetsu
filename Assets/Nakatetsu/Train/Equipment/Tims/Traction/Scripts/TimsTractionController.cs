using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsTractionController : MonoBehaviour
    {
        public static readonly TimsTagKey TargetTractionForcesNKey =
            new("Traction", "TargetTractionForcesN");

        [SerializeField] private TimsCommunicationController communicationController;

        private readonly TimsTractionContext context = new();

        public TimsTractionContext Context => context;
        public TimsTractionOutput Output => context.Output;

        private void Awake()
        {
            ResolveCommunicationController();
        }

        public bool CalculateAndPublish()
        {
            if (!ResolveCommunicationController())
            {
                return false;
            }

            TimsTractionLogic.Calculate(context);
            PublishToMasterBus(communicationController.MasterBus);
            return true;
        }

        private void PublishToMasterBus(TimsBusState masterBus)
        {
            if (!context.Output.hasForceCommand)
            {
                masterBus.Remove(TargetTractionForcesNKey);
                return;
            }

            var targetTractionForcesN =
                new float[context.Output.unitTargetForcesN.Count];
            for (int carIndex = 0;
                 carIndex < context.Output.unitTargetForcesN.Count;
                 carIndex++)
            {
                targetTractionForcesN[carIndex] =
                    context.Output.unitTargetForcesN[carIndex];
            }

            masterBus.SetFloatArray(
                TargetTractionForcesNKey,
                targetTractionForcesN);
        }

        private bool ResolveCommunicationController()
        {
            if (communicationController == null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }

            return communicationController != null;
        }

        public void Configure(TimsCommunicationController controller)
        {
            communicationController = controller;
        }
    }
}
