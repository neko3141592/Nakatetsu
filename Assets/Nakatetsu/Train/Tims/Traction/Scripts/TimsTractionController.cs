using System;
using Nakatetsu.Train.Tims.Communication;
using UnityEngine;


namespace Nakatetsu.Train.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]

    public sealed class TimsTractionController : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController communicationController;

        private readonly TimsTractionContext context = new();

        public TimsTractionContext Context => context;
        public TimsTractionOutput Output => context.Output;

        private void Awake()
        {
            ResolveCommunicationController();
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