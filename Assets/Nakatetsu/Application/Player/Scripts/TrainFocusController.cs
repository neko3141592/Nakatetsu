using System.Collections.Generic;
using Nakatetsu.Train.Integration;
using UnityEngine;

namespace Nakatetsu.Application.Player
{
    public sealed class TrainFocusController : MonoBehaviour
    {
        [SerializeField] private List<TrainIntegrationController> trains;
        [SerializeField] private TrainIntegrationController focusedTrain;
        [SerializeField] private TrainKeyboardInputController keyboardInput;

        public TrainIntegrationController FocusedTrain
        {
            get
            {
                return focusedTrain;
            }
        }

        private void Awake()
        {
            if (trains == null || trains.Count == 0)
            {
                trains = new List<TrainIntegrationController>(
                    Object.FindObjectsByType<TrainIntegrationController>());
            }

            if (keyboardInput == null)
            {
                keyboardInput = GetComponent<TrainKeyboardInputController>();
            }
        }

        private void Start()
        {
            if (focusedTrain != null && keyboardInput != null)
            {
                keyboardInput.SetTarget(focusedTrain);
            }
        }

        public bool TryFocusTrainByTrainId(string trainId)
        {
            if (keyboardInput == null || trains == null)
            {
                return false;
            }

            foreach (var train in trains)
            {
                if (train == null || train.Status == null)
                {
                    continue;
                }

                if (!train.Status.TryGetTrainId(out string currentTrainId))
                {
                    continue;
                }

                if (currentTrainId == trainId)
                {
                    focusedTrain = train;
                    keyboardInput.SetTarget(train);
                    return true;
                }
            }

            return false;
        }
    }
}
