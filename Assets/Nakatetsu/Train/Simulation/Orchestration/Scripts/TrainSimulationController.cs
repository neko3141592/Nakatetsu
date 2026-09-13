using UnityEngine;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Traction;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    public sealed class TrainSimulationController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;

        // 駆動装置
        private readonly Dictionary<int, ITractionEquipment> tractionEquipments = new();

        // ブレーキ装置
        private readonly Dictionary<int, BrakeControlDevice> brakeControllers = new();

        private readonly TrainSimulationContext context = new();

        public TrainRoot TrainRoot => trainRoot;
        public ConsistDefinitionAsset ConsistDefinition =>
            trainRoot != null ? trainRoot.ConsistDefinition : null;
        public IReadOnlyDictionary<int, ITractionEquipment> TractionEquipments => tractionEquipments;
        public IReadOnlyDictionary<int, BrakeControlDevice> BrakeControllers => brakeControllers;
        public TrainSimulationContext Context => context;

        private void Awake()
        {
            ResolveTrainRoot();
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void ResolveTrainRoot()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
        }

        private void ResolveReferences()
        {
            tractionEquipments.Clear();
            foreach (ITractionEquipment tractionEquipment in
                     GetComponentsInChildren<ITractionEquipment>(true))
            {
                if (tractionEquipment is Component component)
                {
                    RegisterEquipment(tractionEquipments, component, tractionEquipment);
                }
            }

            brakeControllers.Clear();
            foreach (BrakeControlDevice brakeController in
                     GetComponentsInChildren<BrakeControlDevice>(true))
            {
                RegisterEquipment(brakeControllers, brakeController, brakeController);
            }
        }

        private void RegisterEquipment<TEquipment>(
            Dictionary<int, TEquipment> destination,
            Component component,
            TEquipment equipment)
        {
            TrainEquipmentAssignment assignment =
                component.GetComponentInParent<TrainEquipmentAssignment>(true);
            if (assignment == null || !assignment.IsAssigned)
            {
                Debug.LogWarning(
                    $"{component.name}に有効な{nameof(TrainEquipmentAssignment)}がありません。",
                    component);
                return;
            }

            int carIndex = assignment.AssignedCarIndex;
            if (!destination.TryAdd(carIndex, equipment))
            {
                Debug.LogError(
                    $"Car {carIndex + 1}に{typeof(TEquipment).Name}が複数割り当てられています。",
                    component);
            }
        }



        private void Update()
        {
            float dt = Time.deltaTime;
            Step(dt);
        }

        private void Step(float deltaTimeSeconds)
        {
            foreach(var pair in tractionEquipments)
            {
                int carIndex = pair.Key;
                ITractionEquipment traction = pair.Value;
                traction.Step(deltaTimeSeconds);
            }

            foreach(var pair in brakeControllers)
            {
                int carIndex = pair.Key;
                BrakeControlDevice brake = pair.Value;
                brake.Step(deltaTimeSeconds);
            }

            PopulateSimulationInput();


        }

        private void PopulateSimulationInput()
        {
            ConsistDefinitionAsset consistDefinition = ConsistDefinition;
            int carCount = consistDefinition != null
                ? consistDefinition.CarCount
                : 0;

            EnsureCarInputCount(carCount);
            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                TrainCarSimulationInput carInput = context.Input.cars[carIndex];
                CarDefinitionAsset carDefinition = consistDefinition.cars[carIndex];

                carInput.carIndex = carIndex;
                carInput.massKg = carDefinition != null
                    ? Mathf.Max(0f, carDefinition.emptyMassKg)
                    : 0f;
                carInput.tractionForceN =
                    tractionEquipments.TryGetValue(carIndex, out ITractionEquipment traction)
                        ? traction.ActualTractionForceN
                        : 0f;
                carInput.brakeForceN =
                    brakeControllers.TryGetValue(carIndex, out BrakeControlDevice brake)
                        ? brake.ActualBrakeForceN
                        : 0f;
            }
        }

        private void EnsureCarInputCount(int carCount)
        {
            List<TrainCarSimulationInput> carInputs = context.Input.cars;
            while (carInputs.Count < carCount)
            {
                carInputs.Add(new TrainCarSimulationInput());
            }

            if (carInputs.Count > carCount)
            {
                carInputs.RemoveRange(carCount, carInputs.Count - carCount);
            }
        }
    }
}
