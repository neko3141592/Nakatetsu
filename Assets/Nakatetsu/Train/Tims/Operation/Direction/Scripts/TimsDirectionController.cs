
using Nakatetsu.Train.Tims.Communication;
using UnityEngine;


namespace Nakatetsu.Train.Tims.Operation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationContext))]
    public sealed class TimsDirectionController : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController communicationController;
        [SerializeField] private TrainRoot trainRoot;

        private void Awake()
        {
            if (communicationController != null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }

            if (trainRoot != null)
            {
                trainRoot = GetComponentInParent<TrainRoot>();
            }
            
        }

        private void Calculate()
        {
            
        }
    }
}