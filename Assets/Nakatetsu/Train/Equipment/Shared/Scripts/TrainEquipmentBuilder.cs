using System.Collections.Generic;
using UnityEngine;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Traction;
using Nakatetsu.Train.Equipment.Traction.Vvvf;

namespace Nakatetsu.Train.Equipment.Shared
{
    [DisallowMultipleComponent]
    public sealed class TrainEquipmentBuilder : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private Transform carsRoot;
        [SerializeField] private bool buildOnAwake = true;

        private readonly List<TrainCarEquipmentInstances> carEquipments = new();

        public TrainRoot TrainRoot => trainRoot;
        public ConsistDefinitionAsset ConsistDefinition =>
            trainRoot != null ? trainRoot.ConsistDefinition : null;
        public Transform CarsRoot => carsRoot;
        public IReadOnlyList<TrainCarEquipmentInstances> CarEquipments => carEquipments;

        private void Awake()
        {
            ResolveTrainRoot();
            if (buildOnAwake)
            {
                Build();
            }
        }

        public void Configure(
            TrainRoot owner,
            Transform explicitCarsRoot = null)
        {
            trainRoot = owner;
            carsRoot = explicitCarsRoot;
        }

        public bool Build()
        {
            ResolveTrainRoot();
            if (!ValidateDefinitions())
            {
                return false;
            }

            ResolveCarsRoot();
            ClearGeneratedEquipment();

            ConsistDefinitionAsset consistDefinition = ConsistDefinition;
            for (int carIndex = 0; carIndex < consistDefinition.CarCount; carIndex++)
            {
                CarDefinitionAsset carDefinition = consistDefinition.cars[carIndex];
                Transform carRoot = GetOrCreateCarRoot(carsRoot, carIndex);
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

                ConfigureGeneratedEquipment(instances, carDefinition);

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
            ConsistDefinitionAsset consistDefinition = ConsistDefinition;
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

                GameObject tractionPrefab = carDefinition.tractionEquipmentPrefab;
                if (tractionPrefab != null &&
                    tractionPrefab.GetComponentInChildren<ITractionEquipment>(true) == null)
                {
                    Debug.LogError(
                        $"{nameof(TrainEquipmentBuilder)}: Car {carIndex + 1}のTraction Equipment Prefabに{nameof(ITractionEquipment)}実装がありません。",
                        tractionPrefab);
                    return false;
                }

                GameObject brakePrefab = carDefinition.brakeEquipmentPrefab;
                if (brakePrefab != null &&
                    brakePrefab.GetComponentInChildren<BrakeControlDevice>(true) == null)
                {
                    Debug.LogError(
                        $"{nameof(TrainEquipmentBuilder)}: Car {carIndex + 1}のBrake Equipment Prefabに{nameof(BrakeControlDevice)}がありません。",
                        brakePrefab);
                    return false;
                }
            }

            return true;
        }

        private bool ResolveTrainRoot()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            return trainRoot != null;
        }

        private static void ConfigureGeneratedEquipment(
            TrainCarEquipmentInstances instances,
            CarDefinitionAsset carDefinition)
        {
            if (instances.TractionEquipment != null &&
                instances.TractionEquipment.TryGetComponent(out VvvfController vvvf))
            {
                vvvf.ConfigureMotor(
                    carDefinition.motorDefinition,
                    carDefinition.driveDefinition,
                    carDefinition.motorCount);
            }

            if (instances.BrakeEquipment != null &&
                instances.BrakeEquipment.TryGetComponent(out BrakeControlDevice brake))
            {
                brake.Configure(
                    carDefinition.brakeCylinderDefinition,
                    carDefinition.brakeCylinderCount);
            }
        }

        private static Transform GetOrCreateCarRoot(Transform parent, int carIndex)
        {
            string carName = $"Car_{carIndex + 1}";
            Transform child = FindDirectChild(parent, carName);
            if (child != null)
            {
                return child;
            }

            // Keep scenes created with the former naming convention compatible.
            child = FindDirectChild(parent, $"Car {carIndex + 1}");
            return child != null ? child : GetOrCreateDirectChild(parent, carName);
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
