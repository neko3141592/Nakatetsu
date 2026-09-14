using System.Collections.Generic;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Simulation.Brake;
using Nakatetsu.Train.Simulation.Load;
using Nakatetsu.Train.Simulation.Traction.Motor;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    [DisallowMultipleComponent]
    public sealed class TrainSimulationBuilder : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private Transform carsRoot;
        [SerializeField] private GameObject defaultMotorSimulationPrefab;
        [SerializeField] private GameObject defaultBrakeSimulationPrefab;
        [SerializeField] private GameObject defaultLoadSimulationPrefab;
        [SerializeField] private bool buildOnAwake = true;

        private readonly List<TrainCarSimulationInstances> carSimulations = new();

        public IReadOnlyList<TrainCarSimulationInstances> CarSimulations => carSimulations;

        private void Awake()
        {
            ResolveTrainRoot();
            if (buildOnAwake)
            {
                Build();
            }
        }

        public bool Build()
        {
            ResolveTrainRoot();
            ConsistDefinitionAsset consist = trainRoot != null
                ? trainRoot.ConsistDefinition
                : null;
            if (consist == null)
            {
                Debug.LogWarning($"{nameof(TrainSimulationBuilder)}: Consist Definitionが設定されていません。", this);
                return false;
            }

            ResolveCarsRoot();
            ClearGeneratedSimulations();
            for (int carIndex = 0; carIndex < consist.CarCount; carIndex++)
            {
                CarDefinitionAsset definition = consist.cars[carIndex];
                if (definition == null)
                {
                    Debug.LogError($"{nameof(TrainSimulationBuilder)}: Car {carIndex + 1}のDefinitionがありません。", this);
                    return false;
                }

                Transform carRoot = GetOrCreateCarRoot(carsRoot, carIndex);
                var instances = new TrainCarSimulationInstances(carIndex, definition, carRoot);

                if (definition.motorCount > 0 && definition.motorDefinition != null)
                {
                    GameObject motorObject = InstantiateSimulation(
                        definition.motorSimulationPrefab != null
                            ? definition.motorSimulationPrefab
                            : defaultMotorSimulationPrefab,
                        carRoot,
                        carIndex);
                    instances.MotorSimulation = motorObject != null
                        ? motorObject.GetComponent<TrainMotorSimulation>()
                        : null;
                    instances.MotorSimulation?.Configure(
                        definition.motorDefinition,
                        definition.driveDefinition,
                        definition.motorCount);
                }

                if (definition.brakeCylinderCount > 0 && definition.brakeCylinderDefinition != null)
                {
                    GameObject brakeObject = InstantiateSimulation(
                        definition.brakeSimulationPrefab != null
                            ? definition.brakeSimulationPrefab
                            : defaultBrakeSimulationPrefab,
                        carRoot,
                        carIndex);
                    instances.BrakeSimulation = brakeObject != null
                        ? brakeObject.GetComponent<TrainBrakeSimulation>()
                        : null;
                    instances.BrakeSimulation?.Configure(
                        definition.brakeCylinderDefinition,
                        definition.brakeCylinderCount);
                }

                GameObject loadObject = InstantiateSimulation(
                    definition.loadSimulationPrefab != null
                        ? definition.loadSimulationPrefab
                        : defaultLoadSimulationPrefab,
                    carRoot,
                    carIndex);
                instances.LoadSimulation = loadObject != null
                    ? loadObject.GetComponent<TrainLoadController>()
                    : null;
                instances.LoadSimulation?.Configure(definition);

                carSimulations.Add(instances);
            }

            return true;
        }

        public void ClearGeneratedSimulations()
        {
            carSimulations.Clear();
            if (carsRoot == null) return;

            TrainSimulationAssignment[] assignments =
                carsRoot.GetComponentsInChildren<TrainSimulationAssignment>(true);
            var generatedObjects = new List<GameObject>();
            foreach (TrainSimulationAssignment assignment in assignments)
            {
                if (assignment != null && assignment.IsGeneratedBy(this) &&
                    !generatedObjects.Contains(assignment.gameObject))
                {
                    generatedObjects.Add(assignment.gameObject);
                }
            }

            for (int i = generatedObjects.Count - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedObjects[i]);
                }
                else
                {
                    DestroyImmediate(generatedObjects[i]);
                }
            }
        }

        private void ResolveTrainRoot()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
        }

        private void ResolveCarsRoot()
        {
            if (carsRoot != null) return;
            carsRoot = FindDirectChild(transform, "Cars");
            if (carsRoot == null)
            {
                var carsObject = new GameObject("Cars");
                carsRoot = carsObject.transform;
                carsRoot.SetParent(transform, false);
            }
        }

        private GameObject InstantiateSimulation(GameObject prefab, Transform parent, int carIndex)
        {
            if (prefab == null) return null;

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            TrainSimulationAssignment assignment =
                instance.GetComponent<TrainSimulationAssignment>();
            if (assignment == null)
            {
                assignment = instance.AddComponent<TrainSimulationAssignment>();
            }

            assignment.AssignGenerated(carIndex, this);
            return instance;
        }

        private static Transform GetOrCreateCarRoot(Transform parent, int carIndex)
        {
            string carName = $"Car_{carIndex + 1}";
            Transform child = FindDirectChild(parent, carName);
            if (child != null) return child;

            var childObject = new GameObject(carName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
            }

            return null;
        }
    }

    public sealed class TrainCarSimulationInstances
    {
        public TrainCarSimulationInstances(
            int carIndex,
            CarDefinitionAsset definition,
            Transform carRoot)
        {
            CarIndex = carIndex;
            Definition = definition;
            CarRoot = carRoot;
        }

        public int CarIndex { get; }
        public CarDefinitionAsset Definition { get; }
        public Transform CarRoot { get; }
        public TrainMotorSimulation MotorSimulation { get; internal set; }
        public TrainBrakeSimulation BrakeSimulation { get; internal set; }
        public TrainLoadController LoadSimulation { get; internal set; }
    }
}
