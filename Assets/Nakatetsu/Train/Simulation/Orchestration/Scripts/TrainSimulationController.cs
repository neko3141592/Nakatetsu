using UnityEngine;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Traction;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using Nakatetsu.Train.Simulation.Physics;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    public sealed class TrainSimulationController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private TrainPhysicsController physicsController;

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
        public TrainPhysicsController PhysicsController => physicsController;

        private void Awake()
        {
            ResolveTrainRoot();
            ResolvePhysicsController();
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

        private void ResolvePhysicsController()
        {
            if (physicsController == null)
            {
                physicsController = GetComponent<TrainPhysicsController>();
            }

            if (physicsController != null)
            {
                physicsController.SetInputSource(context.Input.cars);
            }
        }

        private void ResolveReferences()
        {
            tractionEquipments.Clear();
            foreach (ITractionEquipment tractionEquipment in
                     GetComponentsInChildren<ITractionEquipment>(true))
            {
                if (tractionEquipment is not ISimulationController)
                {
                    Debug.LogError(
                        $"牽引装置が{nameof(ISimulationController)}を実装していません。",
                        tractionEquipment as Object);
                    continue;
                }

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
            float deltaTimeSeconds = Time.deltaTime;
            Step(deltaTimeSeconds);
        }


        private void Step(float deltaTimeSeconds)
        {
            float signedVelocityMps = physicsController != null
                ? physicsController.Context.State.signedVelocityMps
                : 0f;

            foreach(var pair in tractionEquipments)
            {
                ITractionEquipment traction = pair.Value;
                traction.SetVehicleSpeedMps(signedVelocityMps);
                ((ISimulationController)traction).CollectInput();
            }

            foreach(var pair in brakeControllers)
            {
                ISimulationController controller = pair.Value;
                controller.CollectInput();
            }

            foreach(var pair in tractionEquipments)
            {
                ((ISimulationController)pair.Value).Calculate(deltaTimeSeconds);
            }

            foreach(var pair in brakeControllers)
            {
                ISimulationController controller = pair.Value;
                controller.Calculate(deltaTimeSeconds);
            }

            foreach(var pair in tractionEquipments)
            {
                ((ISimulationController)pair.Value).ApplyOutput(deltaTimeSeconds);
            }

            foreach(var pair in brakeControllers)
            {
                ISimulationController controller = pair.Value;
                controller.ApplyOutput(deltaTimeSeconds);
            }

            PopulateSimulationInput();
            if (physicsController != null)
            {
                ISimulationController controller = physicsController;
                controller.CollectInput();
                controller.Calculate(deltaTimeSeconds);
                controller.ApplyOutput(deltaTimeSeconds);
            }
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
