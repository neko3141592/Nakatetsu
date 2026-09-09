using System;
using System.Collections.Generic;
using UnityEngine;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Operation;

namespace Nakatetsu.Train.Equipment
{
    [DisallowMultipleComponent]
    public sealed class TrainEquipmentBuilder : MonoBehaviour
    {
        [SerializeField] private ConsistDefinitionAsset consistDefinition;
        [SerializeField] private Transform carsRoot;
        [SerializeField] private bool buildOnAwake = true;

        private readonly List<TrainCarEquipmentInstances> carEquipments = new();

        public ConsistDefinitionAsset ConsistDefinition => consistDefinition;
        public Transform CarsRoot => carsRoot;
        public IReadOnlyList<TrainCarEquipmentInstances> CarEquipments => carEquipments;

        private void Awake()
        {
            if (buildOnAwake)
            {
                Build();
            }
        }

        public void Configure(ConsistDefinitionAsset definition, Transform explicitCarsRoot = null)
        {
            consistDefinition = definition;
            carsRoot = explicitCarsRoot;
        }

        public bool Build()
        {
            if (!ValidateDefinitions())
            {
                return false;
            }

            ResolveCarsRoot();
            ClearGeneratedEquipment();

            for (int carIndex = 0; carIndex < consistDefinition.CarCount; carIndex++)
            {
                CarDefinitionAsset carDefinition = consistDefinition.cars[carIndex];
                Transform carRoot = GetOrCreateDirectChild(carsRoot, $"Car {carIndex + 1}");
                var instances = new TrainCarEquipmentInstances(carIndex, carDefinition, carRoot);

                instances.MasterController = InstantiateEquipment(
                    carDefinition.masterControllerPrefab,
                    carRoot,
                    carIndex);
                instances.TractionEquipment = InstantiateEquipment(
                    carDefinition.tractionEquipmentPrefab,
                    carRoot,
                    carIndex);
                instances.BrakeEquipment = InstantiateEquipment(
                    carDefinition.brakeEquipmentPrefab,
                    carRoot,
                    carIndex);

                carEquipments.Add(instances);
            }

            return true;
        }

        public void ClearGeneratedEquipment()
        {
            carEquipments.Clear();
            if (carsRoot == null)
            {
                return;
            }

            TrainEquipmentAssignment[] assignments =
                carsRoot.GetComponentsInChildren<TrainEquipmentAssignment>(true);
            var generatedObjects = new List<GameObject>();
            foreach (TrainEquipmentAssignment assignment in assignments)
            {
                if (assignment != null &&
                    assignment.IsGeneratedBy(this) &&
                    !generatedObjects.Contains(assignment.gameObject))
                {
                    generatedObjects.Add(assignment.gameObject);
                }
            }

            for (int i = generatedObjects.Count - 1; i >= 0; i--)
            {
                DestroyGeneratedObject(generatedObjects[i]);
            }
        }

        private bool ValidateDefinitions()
        {
            if (consistDefinition == null)
            {
                Debug.LogWarning($"{nameof(TrainEquipmentBuilder)}: Consist Definitionが設定されていません。", this);
                return false;
            }

            for (int carIndex = 0; carIndex < consistDefinition.CarCount; carIndex++)
            {
                CarDefinitionAsset carDefinition = consistDefinition.cars[carIndex];
                if (carDefinition == null)
                {
                    Debug.LogError(
                        $"{nameof(TrainEquipmentBuilder)}: Car {carIndex + 1}のDefinitionがありません。",
                        this);
                    return false;
                }

                GameObject masterControllerPrefab = carDefinition.masterControllerPrefab;
                if (masterControllerPrefab != null &&
                    masterControllerPrefab.GetComponentInChildren<MasterController>(true) == null)
                {
                    Debug.LogError(
                        $"{nameof(TrainEquipmentBuilder)}: Car {carIndex + 1}のMasterController Prefabに{nameof(MasterController)}がありません。",
                        masterControllerPrefab);
                    return false;
                }
            }

            return true;
        }

        private void ResolveCarsRoot()
        {
            if (carsRoot != null)
            {
                return;
            }

            carsRoot = FindDirectChild(transform, "Cars");
            if (carsRoot == null)
            {
                var carsObject = new GameObject("Cars");
                carsRoot = carsObject.transform;
                carsRoot.SetParent(transform, false);
            }
        }

        private GameObject InstantiateEquipment(GameObject prefab, Transform parent, int carIndex)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;

            TrainEquipmentAssignment assignment =
                instance.GetComponent<TrainEquipmentAssignment>();
            if (assignment == null)
            {
                assignment = instance.AddComponent<TrainEquipmentAssignment>();
            }

            assignment.AssignGenerated(carIndex, this);
            return instance;
        }

        private static Transform GetOrCreateDirectChild(Transform parent, string childName)
        {
            Transform child = FindDirectChild(parent, childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void DestroyGeneratedObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }

    public sealed class TrainCarEquipmentInstances
    {
        public TrainCarEquipmentInstances(
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
        public GameObject MasterController { get; internal set; }
        public GameObject TractionEquipment { get; internal set; }
        public GameObject BrakeEquipment { get; internal set; }
    }
}
