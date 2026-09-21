using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Simulation.Door;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
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
        [SerializeField] private bool AutoRefresh;

        private readonly List<IEquipmentController> equipmentControllers = new();
        private readonly Dictionary<int, SpeedSensor> speedSensors = new();
        private readonly List<IEquipmentInputSourceCollector> equipmentInputSourceCollectors = new();
        private readonly Dictionary<int, ITractionEquipment> tractionEquipments = new();
        private readonly Dictionary<int, BrakeControlDevice> brakeControllers = new();
        private readonly Dictionary<int, TrainMotorSimulation> motorSimulations = new();
        private readonly Dictionary<int, TrainBrakeSimulation> brakeSimulations = new();
        private readonly Dictionary<int, TrainLoadController> loadSimulations = new();
        private readonly Dictionary<int, DoorController> doorControllers = new();
        private readonly Dictionary<int, TrainDoorSimulation> doorSimulations = new();
        private readonly TrainSimulationContext context = new();

        public TrainRoot TrainRoot => trainRoot;
        public ConsistDefinitionAsset ConsistDefinition =>
            trainRoot != null ? trainRoot.ConsistDefinition : null;
        public IReadOnlyList<IEquipmentController> EquipmentControllers => equipmentControllers;
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

            equipmentControllers.Clear();
            doorControllers.Clear();
            speedSensors.Clear();
            equipmentInputSourceCollectors.Clear();
            foreach (MonoBehaviour component in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is SpeedSensor sensor)
                {
                    RegisterEquipment(speedSensors, sensor, sensor);
                }
                else if (component is IEquipmentController controller)
                {
                    equipmentControllers.Add(controller);
                    if (controller is DoorController door) RegisterEquipment(doorControllers, door, door);
                }

                if (component is IEquipmentInputSourceCollector collector)
                {
                    equipmentInputSourceCollectors.Add(collector);
                }
            }

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

            doorSimulations.Clear();
            foreach (TrainDoorSimulation door in searchRoot.GetComponentsInChildren<TrainDoorSimulation>(true))
            {
                RegisterSimulation(doorSimulations, door, door);
            }
            foreach (KeyValuePair<int, DoorController> pair in doorControllers)
            {
                doorSimulations.TryGetValue(pair.Key, out TrainDoorSimulation door);
                pair.Value.SetSimulation(door);
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
            if (AutoRefresh)
            {
                Step(Time.deltaTime);
            }
        }


        public void Step(float deltaTimeSeconds)
        {
            // 測定、機器制御、物理モデル、編成物理の順に1ステップ実行する。
            float signedVelocityMps = physicsController != null
                ? physicsController.Context.State.signedVelocityMps
                : 0f;

            CollectPhysicalMeasurements(signedVelocityMps);
            StepSpeedSensors(deltaTimeSeconds);

            StepEquipment(deltaTimeSeconds);

            StepDoors(deltaTimeSeconds);
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
                CarDefinitionAsset car = ConsistDefinition != null ? ConsistDefinition.cars[pair.Key] : null;
                pair.Value.SetMassMeasurement(loadSimulations.TryGetValue(pair.Key, out TrainLoadController load) && load.IsInitialized
                    ? load.ActualTotalMassKg : car != null ? car.emptyMassKg : 0f);
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
            // TIMSなどの通信入力を各車の入力Busへ先に収集する。
            foreach (IEquipmentInputSourceCollector collector in equipmentInputSourceCollectors)
            {
                collector.CollectInputSources();
            }

            // 全Equipmentの入力を同じ時点で収集する。
            foreach (IEquipmentController controller in equipmentControllers)
            {
                controller.CollectInput();
            }

            // 収集済みの入力から全Equipmentの状態を計算する。
            foreach (IEquipmentController controller in equipmentControllers)
            {
                controller.Calculate(deltaTimeSeconds);
            }

            // 全Equipmentの計算結果を出力へ反映する。
            foreach (IEquipmentController controller in equipmentControllers)
            {
                controller.ApplyOutput(deltaTimeSeconds);
            }
        }

        private void StepSpeedSensors(float deltaTimeSeconds)
        {
            // 前ステップのPhysics Outputを全センサーへ渡し、通信収集前に測定を完了する。
            foreach (KeyValuePair<int, SpeedSensor> pair in speedSensors)
            {
                SpeedSensor sensor = pair.Value;
                if (sensor == null) continue;
                if (physicsController != null && physicsController.isActiveAndEnabled &&
                    ConsistDefinition != null && pair.Key < ConsistDefinition.CarCount)
                {
                    sensor.SetPhysicalSpeedMps(physicsController.Context.Output.signedVelocityMps);
                }
                else
                {
                    sensor.ClearPhysicalSpeed();
                }
                sensor.CollectInput();
            }

            foreach (SpeedSensor sensor in speedSensors.Values)
            {
                if (sensor != null) sensor.Calculate(deltaTimeSeconds);
            }
            foreach (SpeedSensor sensor in speedSensors.Values)
            {
                if (sensor != null) sensor.ApplyOutput(deltaTimeSeconds);
            }
        }

        private void StepDoors(float deltaTimeSeconds)
        {
            // 同じ号車の制御指令を物理モデルへ渡し、各モデルを1回だけ更新する。
            foreach (KeyValuePair<int, TrainDoorSimulation> pair in doorSimulations)
            {
                doorControllers.TryGetValue(pair.Key, out DoorController door);
                pair.Value.SetInput(
                    door != null ? door.LeftCommand : DoorMotionCommand.Hold,
                    door != null ? door.RightCommand : DoorMotionCommand.Hold);
            }
            foreach (TrainDoorSimulation door in doorSimulations.Values)
            {
                door.Calculate(deltaTimeSeconds);
            }
            foreach (TrainDoorSimulation door in doorSimulations.Values)
            {
                door.ApplyOutput(deltaTimeSeconds);
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
                float motorForceN = motorSimulations.TryGetValue(carIndex, out TrainMotorSimulation motor)
                    ? motor.ActualTractionForceN : 0f;
                float regenForceN = 0f;
                if (tractionEquipments.TryGetValue(carIndex, out ITractionEquipment traction) &&
                    traction is VvvfController vvvf)
                {
                    // 機器内の負の力は回生。Physicsの制動力へ渡して前後進共通で速度に逆らわせる。
                    carInput.tractionForceN = Mathf.Max(0f, motorForceN) * vvvf.ForceDirectionSign;
                    regenForceN = Mathf.Max(0f, -motorForceN);
                }
                else carInput.tractionForceN = motorForceN;
                carInput.brakeForceN = (brakeSimulations.TryGetValue(
                    carIndex, out TrainBrakeSimulation brake) ? brake.ActualBrakeForceN : 0f) + regenForceN;
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
