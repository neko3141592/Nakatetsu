using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsNotchController : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController communicationController;
        private TimsNotchContext context;

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (communicationController == null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }
        }




    }
}