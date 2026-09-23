using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Presentation.Bogie;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Shared
{
    public sealed class TrainPresentationBuilder : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField, HideInInspector] private Transform generatedRoot;

        private void Start()
        {
            Generate();
        }

        public void Generate()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            if (trainRoot == null || trainRoot.ConsistDefinition == null)
            {
                return;
            }

            ConsistDefinitionAsset definition = trainRoot.ConsistDefinition;

            int carCount = definition.CarCount;

            if (carCount == 0)
            {
                return;
            }

            for (int i = 0; i < carCount; i++)
            {
                if (trainRoot.ConsistDefinition.cars[i] == null || definition.cars[i].presentationPrefab == null)
                {
                    return;
                }
            }

            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                GameObject CarPrefab = definition.cars[carIndex].presentationPrefab;
                GameObject car = Instantiate(CarPrefab, transform, false);
                car.name = $"Car_{carIndex + 1:00}_{CarPrefab.name}";
                var assignment = car.GetComponent<TrainPresentationAssignment>();
                if (assignment == null)
                {
                    assignment = car.AddComponent<TrainPresentationAssignment>();
                }

                assignment.AssignCarIndex(carIndex);

                if (car.GetComponent<TrainBogiePresentation>() == null)
                {
                    car.AddComponent<TrainBogiePresentation>();
                }
            }
        }
    }
}
