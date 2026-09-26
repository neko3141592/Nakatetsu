using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainInputController), typeof(TrainStatusController), typeof(TrainCabInitializer))]
    public sealed class TrainIntegrationController : MonoBehaviour
    {
        private TrainInputController inputController;
        private TrainStatusController statusController;
        private TrainCabInitializer cabInitializer;
        private TrainRoot trainRoot;

        public TrainInputController Input
        {
            get
            {
                return inputController;
            }
        }

        public TrainStatusController Status
        {
            get
            {
                return statusController;
            }
        }

        private void Awake()
        {
            trainRoot = GetComponentInParent<TrainRoot>(true);
            if (trainRoot == null)
            {
                Debug.LogError("TrainIntegrationControllerはTrainRootの配下に配置してください。", this);
                enabled = false;
                return;
            }

            inputController = GetComponent<TrainInputController>();
            statusController = GetComponent<TrainStatusController>();
            cabInitializer = GetComponent<TrainCabInitializer>();

            var resolver = new TrainCabResolver(trainRoot);
            inputController.Initialize(resolver);
            statusController.Initialize(resolver);
        }

        private void Start()
        {
            if (cabInitializer.isActiveAndEnabled)
            {
                cabInitializer.Initialize(trainRoot);
            }
        }
    }
}
