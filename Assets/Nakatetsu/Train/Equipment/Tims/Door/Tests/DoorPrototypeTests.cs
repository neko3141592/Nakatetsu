using System.Reflection;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Traction;
using Nakatetsu.Train.Simulation.Brake;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Simulation.Door;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class DoorPrototypeTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset car;
        private TimsCommunicationController communication;
        private TrainSimulationController simulation;
        private TrainPhysicsController physics;
        private DoorController[] doors;

        [SetUp]
        public void SetUp()
        {
            train = new GameObject("DoorTestTrain");
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            car.emptyMassKg = 30000f;
            car.additionalEquipmentPrefabs = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nakatetsu/Train/Equipment/Door/Prefabs/DoorPrototype.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nakatetsu/Train/Equipment/SpeedSensor/Prefabs/SpeedSensor.prefab")
            };
            Assert.That(car.additionalEquipmentPrefabs[0], Is.Not.Null);
            consist.cars.Add(car); consist.cars.Add(car);
            var root = train.AddComponent<TrainRoot>(); root.Configure(consist);
            var builder = train.AddComponent<TrainEquipmentBuilder>(); builder.Configure(root);
            Assert.That(builder.Build(), Is.True);
            doors = train.GetComponentsInChildren<DoorController>();
            communication = train.AddComponent<TimsCommunicationController>();
            Invoke(communication, "Awake");
            physics = train.AddComponent<TrainPhysicsController>();
            simulation = train.AddComponent<TrainSimulationController>();
            Invoke(simulation, "Awake"); simulation.ResolveReferences();
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train); Object.DestroyImmediate(consist); Object.DestroyImmediate(car);
        }
        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private void Step(float seconds) => Invoke(simulation, "Step", seconds);
        private void Collect() => communication.CollectInputSources();
        private bool Master(Nakatetsu.Train.Equipment.Tims.Bus.TimsTagKey key)
        {
            Assert.That(communication.MasterBus.TryGetBool(key, out bool value), Is.True);
            return value;
        }

        [Test]
        public void PrefabBuildsFourDoorsPerSideAndUnknownDoesNotMeanClosed()
        {
            Assert.That(doors.Length, Is.EqualTo(2));
            Collect();
            Assert.That(Master(TimsDoorController.HasValidStateKey), Is.False);
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.False);
            Step(0f); Collect();
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.True);
            foreach (var door in doors)
                Assert.That(door.GetComponent<TrainDoorSimulation>().DoorsPerSide, Is.EqualTo(4));
        }

        [Test]
        public void LeftDoorsMoveOncePerStepAndRightStaysClosed()
        {
            Step(0f); Collect();
            doors[0].OpenLeft();
            // 接点がまだ閉でも開指令受付中は力行を許可しない。
            Collect();
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.True);
            Assert.That(Master(TimsDoorController.TractionPermittedKey), Is.False);
            Step(1.5f); Collect();
            var model = doors[0].GetComponent<TrainDoorSimulation>();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(model.TryGetDoor(true, i, out float opening, out bool closed, out DoorStatus status), Is.True);
                Assert.That(opening, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(closed, Is.False); Assert.That(status, Is.EqualTo(DoorStatus.Opening));
                model.TryGetDoor(false, i, out float right, out bool rc, out _);
                Assert.That(right, Is.Zero); Assert.That(rc, Is.True);
            }
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.False);
            Assert.That(communication.GetLocalBus(0).TryGetIntArray(DoorTimsBusSource.LeftStatusKey, out int[] states), Is.True);
            Assert.That(states, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(communication.MasterBus.TryGetIntArray(TimsDoorController.CarClosedStatesKey, out int[] cars), Is.True);
            Assert.That(cars, Is.EqualTo(new[] { 0, 1 }));
            doors[0].CloseBoth(); Step(1.5f); Collect();
            Assert.That(Master(TimsDoorController.TractionPermittedKey), Is.True);
        }

        [TestCase(5f)]
        [TestCase(-5f)]
        public void MovingTrainRejectsOpeningAndDoesNotReplayRejectedRequest(float speed)
        {
            Step(0f);
            physics.Context.State.signedVelocityMps = speed; physics.ApplyOutput(0f);
            doors[0].OpenLeft(); Step(1f);
            physics.Context.State.signedVelocityMps = 0f; physics.ApplyOutput(0f);
            Step(1f); Collect();
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.True);
            doors[0].OpenLeft(); Step(1f); Collect();
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.False);
        }

        [Test]
        public void MissingSpeedBlocksOpeningButStillAllowsClosing()
        {
            Step(0f); doors[0].OpenLeft(); Step(1f);
            physics.enabled = false;
            Step(1f);
            var model = doors[0].GetComponent<TrainDoorSimulation>();
            model.TryGetDoor(true, 0, out float opening, out _, out DoorStatus state);
            Assert.That(opening, Is.EqualTo(1f / 3f).Within(0.00001f));
            Assert.That(state, Is.EqualTo(DoorStatus.Stopped));
            doors[0].CloseBoth(); Step(1f); Collect();
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.True);
        }

        [Test]
        public void ClosingCanBeReopenedOnlyOnPermittedSide()
        {
            Step(0f); doors[0].OpenLeft(); Step(3f);
            doors[0].CloseBoth(); Step(1f);
            doors[0].SetOpeningPermissions(true, false);
            doors[0].OpenLeft(); doors[0].OpenRight(); Step(1f);
            var model = doors[0].GetComponent<TrainDoorSimulation>();
            model.TryGetDoor(true, 0, out float left, out _, out _);
            model.TryGetDoor(false, 0, out float right, out _, out _);
            Assert.That(left, Is.EqualTo(1f)); Assert.That(right, Is.Zero);
        }

        [TestCase("source")]
        [TestCase("simulation")]
        [TestCase("controller")]
        [TestCase("assignment")]
        public void LossOfOneCarNeverLeavesAllClosed(string failure)
        {
            Step(0f); Collect(); Assert.That(Master(TimsDoorController.AllClosedKey), Is.True);
            if (failure == "source") doors[1].GetComponent<DoorTimsBusSource>().enabled = false;
            if (failure == "simulation") doors[1].GetComponent<TrainDoorSimulation>().enabled = false;
            if (failure == "controller") doors[1].enabled = false;
            if (failure == "assignment") doors[1].GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1);
            Collect();
            Assert.That(Master(TimsDoorController.HasValidStateKey), Is.False);
            Assert.That(Master(TimsDoorController.TractionPermittedKey), Is.False);
        }

        [Test]
        public void FaultAtClosedPositionIsNotAClosedContact()
        {
            Step(0f); Collect();
            var model = doors[0].GetComponent<TrainDoorSimulation>();
            typeof(TrainDoorSimulation).GetField("fault", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, true);
            Step(0.1f); Collect();
            Assert.That(Master(TimsDoorController.HasValidStateKey), Is.True);
            Assert.That(Master(TimsDoorController.HasFaultKey), Is.True);
            Assert.That(Master(TimsDoorController.AllClosedKey), Is.False);
        }

        [Test]
        public void MonitorDisableRevokesPermissionOnCollection()
        {
            Step(0f); Collect();
            communication.GetComponent<TimsDoorController>().enabled = false;
            Collect();
            Assert.That(Master(TimsDoorController.TractionPermittedKey), Is.False);
        }

        [Test]
        public void TimsPipelineCutsTractionOnOpenRequestAndRestoresAfterClosure()
        {
            Step(0f); Collect();
            var settings = ScriptableObject.CreateInstance<TimsSettingsAsset>();
            var cylinder = ScriptableObject.CreateInstance<BrakeCylinderDefinitionAsset>();
            try
            {
                for (int i = 0; i < settings.brakeNotchCount; i++) settings.brakeTargetDecelerationsKmhPerSec.Add(i + 1f);
                car.motorCount = 0;
                car.brakeCylinderDefinition = cylinder;
                car.brakeCylinderCount = 1;
                var control = train.AddComponent<TimsControlController>();
                train.GetComponent<TimsRoot>().Configure(settings);
                for (int i = 0; i < 2; i++)
                {
                    var local = communication.GetLocalBus(i);
                    local.SetInt(CabActivationSwitchTimsBusSource.SwitchPositionKey, i == 0 ? 1 : -1);
                    local.SetInt(MasterControllerTimsBusSource.ReverserPositionKey, 1);
                    local.SetInt(MasterControllerTimsBusSource.PowerPositionKey, 1);
                    local.SetInt(MasterControllerTimsBusSource.BrakePositionKey, 0);
                    local.SetFloat(BrakeControlDeviceTimsBusSource.MassKgKey, 30000f);
                    local.SetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey, 0f);
                    local.SetFloat(BrakeControlDeviceTimsBusSource.ActualForceNKey, 0f);
                    local.SetFloat(BrakeControlDeviceTimsBusSource.ForcePerKPaKey, 10f);
                    local.SetFloat(BrakeControlDeviceTimsBusSource.MaximumPressureKPaKey, 300f);
                }
                Collect();
                var traction = train.GetComponent<TimsTractionController>();
                Assert.That(traction.Context.Input.isReady, Is.True, control.EmergencyReason);
                Assert.That(traction.Context.Input.powerNotch, Is.EqualTo(1));
                doors[0].OpenLeft(); Collect();
                Assert.That(traction.Context.Input.isReady, Is.True);
                Assert.That(traction.Context.Input.powerNotch, Is.Zero);
                Assert.That(traction.Output.targetForceN, Is.Zero);
                Step(1f); Collect();
                Assert.That(traction.Context.Input.powerNotch, Is.Zero);
                doors[0].CloseBoth(); Step(1f); Collect();
                Assert.That(traction.Context.Input.powerNotch, Is.EqualTo(1));
                doors[1].GetComponent<DoorTimsBusSource>().enabled = false; Collect();
                Assert.That(traction.Context.Input.powerNotch, Is.Zero);
            }
            finally { Object.DestroyImmediate(settings); Object.DestroyImmediate(cylinder); }
        }

        [Test]
        public void DifferentTimePartitionsProduceSameOpening()
        {
            var a = new DoorSimulationState(); var b = new DoorSimulationState();
            DoorSimulationLogic.Step(a, DoorMotionCommand.Open, 3f, 1.5f, false);
            for (int i = 0; i < 15; i++) DoorSimulationLogic.Step(b, DoorMotionCommand.Open, 3f, 0.1f, false);
            Assert.That(a.openingRatio, Is.EqualTo(b.openingRatio).Within(0.00001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidTimeDoesNotMoveOrReportClosed(float dt)
        {
            var state = new DoorSimulationState();
            Assert.That(DoorSimulationLogic.Step(state, DoorMotionCommand.Open, 3f, dt, false), Is.False);
            Assert.That(state.openingRatio, Is.Zero); Assert.That(state.closedContact, Is.False);
        }
    }
}
