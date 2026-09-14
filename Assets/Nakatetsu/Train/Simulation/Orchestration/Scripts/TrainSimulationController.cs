using System.Collections.Generic;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Traction;
using Nakatetsu.Train.Equipment.Traction.Vvvf;
using Nakatetsu.Train.Simulation.Brake;
using Nakatetsu.Train.Simulation.Load;
using Nakatetsu.Train.Simulation.Orchestration.Interfaces;
using Nakatetsu.Train.Simulation.Physics;
using Nakatetsu.Train.Simulation.Traction.Motor;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.Orchestration
{
    public sealed class TrainSimulationController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private TrainPhysicsController physicsController;

        private readonly Dictionary<int, ITractionEquipment> tractionEquipments = new();
        private readonly Dictionary<int, BrakeControlDevice> brakeControllers = new();
        private readonly Dictionary<int, TrainMotorSimulation> motorSimulations = new();
        private readonly Dictionary<int, TrainBrakeSimulation> brakeSimulations = new();
        private readonly Dictionary<int, TrainLoadController> loadSimulations = new();
        private readonly TrainSimulationContext context = new();

        public TrainRoot TrainRoot => trainRoot;
        public ConsistDefinitionAsset ConsistDefinition =>
            trainRoot != null ? trainRoot.ConsistDefinition : null;
        public IReadOnlyDictionary<int, ITractionEquipment> TractionEquipments => tractionEquipments;
        public IReadOnlyDictionary<int, BrakeControlDevice> BrakeControllers => brakeControllers;
        public IReadOnlyDictionary<int, TrainMotorSimulation> MotorSimulations => motorSimulations;
        public IReadOnlyDictionary<int, TrainBrakeSimulation> BrakeSimulations => brakeSimulations;
        public IReadOnlyDictionary<int, TrainLoadController> LoadSimulations => loadSimulations;
        public TrainSimulationContext Context => context;
        public TrainPhysicsController PhysicsController => physicsController;

        private void Awake()
        {
            // 編成ルートと物理Controllerの参照を解決する。
            ResolveTrainRoot();
            ResolvePhysicsController();
        }

        private void Start()
        {
            // 生成済みのEquipmentとSimulationを車両ごとに収集する。
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            // 編成内のEquipmentとSimulationをcarIndex別に登録し直す。
            Transform searchRoot = trainRoot != null ? trainRoot.transform : transform;

            tractionEquipments.Clear();
            foreach (MonoBehaviour component in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is ITractionEquipment traction && component is IEquipmentController)
                {
                    RegisterEquipment(tractionEquipments, component, traction);
                }
            }

            brakeControllers.Clear();
            foreach (BrakeControlDevice brake in searchRoot.GetComponentsInChildren<BrakeControlDevice>(true))
            {
                RegisterEquipment(brakeControllers, brake, brake);
            }

            motorSimulations.Clear();
            foreach (TrainMotorSimulation motor in searchRoot.GetComponentsInChildren<TrainMotorSimulation>(true))
            {
                RegisterSimulation(motorSimulations, motor, motor);
            }

            brakeSimulations.Clear();
            foreach (TrainBrakeSimulation brake in searchRoot.GetComponentsInChildren<TrainBrakeSimulation>(true))
            {
                RegisterSimulation(brakeSimulations, brake, brake);
            }

            loadSimulations.Clear();
            foreach (TrainLoadController load in searchRoot.GetComponentsInChildren<TrainLoadController>(true))
            {
                RegisterSimulation(loadSimulations, load, load);
            }
        }

        private void Update()
        {
            // 1フレーム分の編成シミュレーションを進める。
            Step(Time.deltaTime);
        }

        private void Step(float deltaTimeSeconds)
        {
            // 測定、機器制御、物理モデル、編成物理の順に1ステップ実行する。
            float signedVelocityMps = physicsController != null
                ? physicsController.Context.State.signedVelocityMps
                : 0f;

            CollectPhysicalMeasurements(signedVelocityMps);

            StepEquipment(deltaTimeSeconds);

            StepPhysicalSimulation(signedVelocityMps, deltaTimeSeconds);
            PopulatePhysicsInput();

            if (physicsController != null)
            {
                physicsController.SetInput(context.Input.cars);
                ISimulationController controller = physicsController;
                controller.Calculate(deltaTimeSeconds);
                controller.ApplyOutput(deltaTimeSeconds);
            }
        }

        private void CollectPhysicalMeasurements(float signedVelocityMps)
        {
            // 前ステップの物理値をEquipmentの測定値として渡す。
            foreach (KeyValuePair<int, ITractionEquipment> pair in tractionEquipments)
            {
                pair.Value.SetVehicleSpeedMps(signedVelocityMps);
                if (pair.Value is VvvfController vvvf &&
                    motorSimulations.TryGetValue(pair.Key, out TrainMotorSimulation motor))
                {
                    vvvf.SetMotorMeasurement(
                        motor.AverageMotorTorqueNm,
                        motor.ActualTractionForceN,
                        motor.TotalMotorCurrentRmsA,
                        motor.TotalMotorOutputPowerW);
                }
            }

            foreach (KeyValuePair<int, BrakeControlDevice> pair in brakeControllers)
            {
                if (brakeSimulations.TryGetValue(pair.Key, out TrainBrakeSimulation brake))
                {
                    pair.Value.SetSimulationMeasurement(
                        brake.ActualBrakeForceN,
                        brake.OperationalCylinderCount);
                }
            }
        }

        private void StepEquipment(float deltaTimeSeconds)
        {
            // 全Equipmentを入力収集、計算、出力適用の順に更新する。
            foreach (ITractionEquipment traction in tractionEquipments.Values)
            {
                ((IEquipmentController)traction).CollectInput();
            }

            foreach (BrakeControlDevice brake in brakeControllers.Values)
            {
                brake.CollectInput();
            }

            foreach (ITractionEquipment traction in tractionEquipments.Values)
            {
                ((IEquipmentController)traction).Calculate(deltaTimeSeconds);
            }

            foreach (BrakeControlDevice brake in brakeControllers.Values)
            {
                brake.Calculate(deltaTimeSeconds);
            }

            foreach (ITractionEquipment traction in tractionEquipments.Values)
            {
                ((IEquipmentController)traction).ApplyOutput(deltaTimeSeconds);
            }

            foreach (BrakeControlDevice brake in brakeControllers.Values)
            {
                brake.ApplyOutput(deltaTimeSeconds);
            }
        }

        private void StepPhysicalSimulation(float signedVelocityMps, float deltaTimeSeconds)
        {
            // Equipmentの指令をMotor・Brake・Loadの物理モデルへ反映する。
            foreach (KeyValuePair<int, TrainMotorSimulation> pair in motorSimulations)
            {
                if (tractionEquipments.TryGetValue(pair.Key, out ITractionEquipment traction) &&
                    traction is VvvfController vvvf)
                {
                    pair.Value.SetInput(
                        vvvf.Output.lineVoltageRmsV,
                        vvvf.Output.frequencyHz,
                        signedVelocityMps);
                }
                else
                {
                    pair.Value.SetInput(0f, 0f, signedVelocityMps);
                }

                pair.Value.Calculate(deltaTimeSeconds);
                pair.Value.ApplyOutput(deltaTimeSeconds);
            }

            foreach (KeyValuePair<int, TrainBrakeSimulation> pair in brakeSimulations)
            {
                float targetPressureKPa = brakeControllers.TryGetValue(
                    pair.Key,
                    out BrakeControlDevice controller)
                    ? controller.TargetPressureKPa
                    : 0f;
                pair.Value.SetTargetPressureKPa(targetPressureKPa);
                pair.Value.Calculate(deltaTimeSeconds);
                pair.Value.ApplyOutput(deltaTimeSeconds);
            }

            foreach (TrainLoadController load in loadSimulations.Values)
            {
                load.Calculate(deltaTimeSeconds);
                load.ApplyOutput(deltaTimeSeconds);
            }
        }

        private void PopulatePhysicsInput()
        {
            // 車両ごとの質量・駆動力・制動力を編成物理入力へまとめる。
            ConsistDefinitionAsset consist = ConsistDefinition;
            int carCount = consist != null ? consist.CarCount : 0;
            EnsureCarInputCount(carCount);

            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                TrainCarSimulationInput carInput = context.Input.cars[carIndex];
                CarDefinitionAsset definition = consist.cars[carIndex];
                carInput.carIndex = carIndex;
                carInput.massKg = loadSimulations.TryGetValue(carIndex, out TrainLoadController load)
                    ? load.ActualTotalMassKg
                    : definition != null ? Mathf.Max(0f, definition.emptyMassKg) : 0f;
                carInput.tractionForceN = motorSimulations.TryGetValue(
                    carIndex,
                    out TrainMotorSimulation motor)
                    ? motor.ActualTractionForceN
                    : 0f;
                carInput.brakeForceN = brakeSimulations.TryGetValue(
                    carIndex,
                    out TrainBrakeSimulation brake)
                    ? brake.ActualBrakeForceN
                    : 0f;
                carInput.externalForceN = 0f;
            }
        }

        private void RegisterEquipment<T>(Dictionary<int, T> destination, Component component, T value)
        {
            // EquipmentAssignmentからcarIndexを取得して機器を登録する。
            TrainEquipmentAssignment assignment =
                component.GetComponentInParent<TrainEquipmentAssignment>(true);
            if (assignment == null || !assignment.IsAssigned)
            {
                Debug.LogWarning($"{component.name}に有効な{nameof(TrainEquipmentAssignment)}がありません。", component);
                return;
            }

            Register(destination, assignment.AssignedCarIndex, component, value);
        }

        private void RegisterSimulation<T>(Dictionary<int, T> destination, Component component, T value)
        {
            // SimulationAssignmentからcarIndexを取得して物理モデルを登録する。
            TrainSimulationAssignment assignment =
                component.GetComponentInParent<TrainSimulationAssignment>(true);
            if (assignment == null || !assignment.IsAssigned)
            {
                Debug.LogWarning($"{component.name}に有効な{nameof(TrainSimulationAssignment)}がありません。", component);
                return;
            }

            Register(destination, assignment.AssignedCarIndex, component, value);
        }

        private static void Register<T>(
            Dictionary<int, T> destination,
            int carIndex,
            Component component,
            T value)
        {
            // 同じ車両への重複登録を検出しながら辞書へ追加する。
            if (!destination.TryAdd(carIndex, value))
            {
                Debug.LogError(
                    $"Car {carIndex + 1}に{typeof(T).Name}が複数割り当てられています。",
                    component);
            }
        }

        private void ResolveTrainRoot()
        {
            // 未設定のTrainRootを親階層から取得する。
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }
        }

        private void ResolvePhysicsController()
        {
            // 未設定の物理Controllerを編成階層から取得する。
            if (physicsController == null)
            {
                Transform searchRoot = trainRoot != null ? trainRoot.transform : transform;
                physicsController = searchRoot.GetComponentInChildren<TrainPhysicsController>(true);
            }
        }

        private void EnsureCarInputCount(int carCount)
        {
            // 編成物理入力の要素数を現在の車両数に合わせる。
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
