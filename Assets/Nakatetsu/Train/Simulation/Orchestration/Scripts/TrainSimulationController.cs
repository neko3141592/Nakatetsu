using UnityEngine;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Traction;
using Codice.CM.Common.Merge;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    public sealed class TrainSimulationController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;

        // 駆動装置
        private readonly Dictionary<int, ITractionEquipment> tractionEquipments = new();

        // ブレーキ装置
        private readonly Dictionary<int, BrakeControlDevice> brakeControllers = new();

        public TrainRoot TrainRoot => trainRoot;
        public ConsistDefinitionAsset ConsistDefinition =>
            trainRoot != null ? trainRoot.ConsistDefinition : null;
        public IReadOnlyDictionary<int, ITractionEquipment> TractionEquipments => tractionEquipments;
        public IReadOnlyDictionary<int, BrakeControlDevice> BrakeControllers => brakeControllers;

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

            float currentTractionForce = 0;
        }
    }
}
