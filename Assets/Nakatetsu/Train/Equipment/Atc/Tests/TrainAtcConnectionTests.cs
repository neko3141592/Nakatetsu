using System.Reflection;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Simulation.Atc;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.TrackPosition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nakatetsu.Train.Equipment.Atc.Tests
{
    public sealed class TrainAtcConnectionTests
    {
        private const string ReceiverPrefabPath = "Assets/Nakatetsu/Train/Simulation/Atc/Prefabs/TrainAtcReceiver.prefab";
        private const string AtcPrefabPath = "Assets/Nakatetsu/Train/Equipment/Atc/Prefabs/TrainAtc.prefab";
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset car;
        private TrainSimulationBuilder simulationBuilder;
        private TrainEquipmentBuilder equipmentBuilder;
        private TrainSimulationController simulation;
        private TrainAtcController atc;
        private TimsDirectionController direction;

        [SetUp]
        public void SetUp()
        {
            train = new GameObject("Train");
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            car.carType = CarType.Tc1;
            car.atcReceiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReceiverPrefabPath);
            car.additionalEquipmentPrefabs = new GameObject[0];
            consist.cars.Add(car);
            consist.cars.Add(car);
            consist.commonEquipmentPrefabs = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(AtcPrefabPath) };
            train.AddComponent<TrainRoot>().Configure(consist);
            simulationBuilder = train.AddComponent<TrainSimulationBuilder>();
            equipmentBuilder = train.AddComponent<TrainEquipmentBuilder>();
            Assert.That(simulationBuilder.Build(), Is.True);
            Assert.That(equipmentBuilder.Build(), Is.True);
            atc = train.GetComponentInChildren<TrainAtcController>();
            var tims = new GameObject("Tims");
            tims.transform.SetParent(train.transform, false);
            direction = tims.AddComponent<TimsDirectionController>();
            direction.Output.activatedCabPosition = ActivatedCabPosition.Front;
            CreateMaster(0);
            CreateMaster(1);
            simulation = train.AddComponent<TrainSimulationController>();
            typeof(TrainSimulationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(simulation, null);
            simulation.ResolveReferences();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(consist);
            Object.DestroyImmediate(car);
        }

        [Test]
        public void SingleAtcReadsBothReceiversImmediately()
        {
            simulationBuilder.CarSimulations[0].AtcReceiver.Context.Output.telegram =
                new TrackCircuitAtcTelegram { issuedAtSeconds = 10d, isValid = true };
            simulationBuilder.CarSimulations[1].AtcReceiver.Context.Output.telegram =
                new TrackCircuitAtcTelegram { issuedAtSeconds = 11d, isValid = false };
            atc.CollectInput();
            atc.Calculate(0.01f);
            Assert.That(atc.Context.State.operation.currentTelegram.issuedAtSeconds, Is.EqualTo(10d));
            Assert.That(atc.Context.State.operation.currentTelegram.isValid, Is.True);
            Assert.That(atc.Context.Input.rearTelegram.issuedAtSeconds, Is.EqualTo(11d));
            Assert.That(atc.Context.Input.rearTelegram.isValid, Is.False);

            direction.Output.activatedCabPosition = ActivatedCabPosition.Rear;
            atc.CollectInput();
            atc.Calculate(0.01f);
            Assert.That(atc.Context.State.operation.currentTelegram.issuedAtSeconds, Is.EqualTo(11d));
            Assert.That(atc.Context.State.operation.currentTelegram.isValid, Is.False);

            simulationBuilder.CarSimulations[0].AtcReceiver.Context.Output.telegram =
                new TrackCircuitAtcTelegram { issuedAtSeconds = 12d };
            atc.CollectInput();
            Assert.That(atc.Context.Input.frontTelegram.issuedAtSeconds, Is.EqualTo(12d));
            Assert.That(atc.Context.Input.rearTelegram.issuedAtSeconds, Is.EqualTo(11d));
        }

        [Test]
        public void MissingOrDisabledReceiverClearsOnlyItsSide()
        {
            var receiver = simulationBuilder.CarSimulations[0].AtcReceiver;
            receiver.Context.Output.telegram = new TrackCircuitAtcTelegram { isValid = true };
            simulationBuilder.CarSimulations[1].AtcReceiver.Context.Output.telegram =
                new TrackCircuitAtcTelegram { isValid = true };
            atc.CollectInput();
            atc.Calculate(0.01f);
            receiver.enabled = false;
            atc.CollectInput();
            atc.Calculate(0.01f);
            Assert.That(atc.Context.Input.frontTelegram, Is.Null);
            Assert.That(atc.Context.State.operation.currentTelegram, Is.Null);
            Assert.That(atc.Context.Input.rearTelegram, Is.Not.Null);
            Object.DestroyImmediate(receiver.gameObject);
            simulation.ResolveReferences();
            simulationBuilder.CarSimulations[1].AtcReceiver.Context.Output.telegram =
                new TrackCircuitAtcTelegram { isValid = true };
            atc.CollectInput();
            Assert.That(atc.Context.Input.frontTelegram, Is.Null);
            Assert.That(atc.Context.Input.rearTelegram, Is.Not.Null);
        }

        [Test]
        public void SimulationUpdatesReceiversBeforeCollectingEquipmentInput()
        {
            // 線路位置が未設定なので、受信機更新で前回電文が消えてから車上ATCが読む。
            foreach (var carSimulation in simulationBuilder.CarSimulations)
            {
                carSimulation.AtcReceiver.Context.Output.telegram = new TrackCircuitAtcTelegram { isValid = true };
            }
            LogAssert.Expect(LogType.Error, "車上ATCの初期設定に必要なATCグラフまたは両端の受信機がありません。");
            simulation.Calculate(0.01f);
            Assert.That(atc.Context.Input.frontTelegram, Is.Null);
            Assert.That(atc.Context.State.operation.currentTelegram, Is.Null);
            Assert.That(atc.Context.Input.rearTelegram, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstStepInitializesMountedReceiverPositionsOnlyOnce(bool frontFacesAtoB)
        {
            var position = ConfigureInitialTrackPosition(frontFacesAtoB);
            foreach (var carSimulation in simulationBuilder.CarSimulations)
            {
                var receiver = new SerializedObject(carSimulation.AtcReceiver);
                receiver.FindProperty("offsetFromCarCenterM").floatValue = 2f;
                receiver.ApplyModifiedPropertiesWithoutUndo();
            }
            simulation.Calculate(0f);
            Assert.That(atc.Context.State.position.isPositionInitialized, Is.True);
            Assert.That(atc.Context.Graph, Is.SameAs(AssetDatabase.LoadAssetAtPath<TrackAtcGraphAsset>(
                "Assets/Nakatetsu/Track/NtLine/Data/NtLineAtcGraph.asset").Definition));
            float directionSign = frontFacesAtoB ? 1f : -1f;
            Assert.That(atc.Context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(400f + 2f * directionSign));
            Assert.That(atc.Context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(400f - 18f * directionSign));
            Assert.That(atc.Context.State.position.frontPosition.frontFacesAtoB, Is.EqualTo(frontFacesAtoB));
            Assert.That(atc.Context.State.position.rearPosition.frontFacesAtoB, Is.EqualTo(frontFacesAtoB));
            var front = atc.Context.State.position.frontPosition;
            var rear = atc.Context.State.position.rearPosition;

            // 物理位置・向きを変えて再接続しても、初期設定済みの車上位置を読み直さない。
            position.SetTrackPosition("nt-approach", 500f, !frontFacesAtoB);
            simulation.ResolveReferences();
            direction.Output.activatedCabPosition = ActivatedCabPosition.Rear;
            simulation.Calculate(0f);
            Assert.That(atc.Context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(front.distanceOnAtcEdgeM));
            Assert.That(atc.Context.State.position.frontPosition.frontFacesAtoB, Is.EqualTo(front.frontFacesAtoB));
            Assert.That(atc.Context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(rear.distanceOnAtcEdgeM));
            Assert.That(atc.Context.State.position.rearPosition.frontFacesAtoB, Is.EqualTo(rear.frontFacesAtoB));
            Assert.That(atc.Context.State.operation.currentPosition.distanceOnAtcEdgeM, Is.EqualTo(rear.distanceOnAtcEdgeM));
            Assert.That(atc.Context.State.operation.currentPosition.frontFacesAtoB, Is.EqualTo(rear.frontFacesAtoB));
        }

        [Test]
        public void FailedStartupDoesNotReadPhysicalPositionOnLaterSteps()
        {
            LogAssert.Expect(LogType.Error, "車上ATCの初期設定に必要なATCグラフまたは両端の受信機がありません。");
            simulation.Calculate(0f);
            ConfigureInitialTrackPosition(true);
            simulation.Calculate(0f);
            Assert.That(atc.Context.State.position.isPositionInitialized, Is.False);
            Assert.That(atc.Context.State.isAtcHealthy, Is.False);
            Assert.That(atc.Context.State.operation.hasCurrentPosition, Is.False);
        }

        [Test]
        public void SimulationConnectsSpeedSensorAndAtcTracksWhileKeyIsOff()
        {
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "edge", lengthM = 100f });
            Assert.That(atc.TryInitializePosition(graph,
                new TrainAtcPosition { atcEdgeId = "edge", distanceOnAtcEdgeM = 50f, frontFacesAtoB = true },
                new TrainAtcPosition { atcEdgeId = "edge", distanceOnAtcEdgeM = 20f, frontFacesAtoB = true }), Is.True);
            var sensorObject = new GameObject("SpeedSensor");
            sensorObject.transform.SetParent(train.transform, false);
            var sensor = sensorObject.AddComponent<SpeedSensor>();
            sensor.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(0);
            simulation.ResolveReferences();
            foreach (var master in train.GetComponentsInChildren<MasterController>())
            {
                Assert.That(master.SetReverserPosition(ReverserPosition.Neutral), Is.True);
                Assert.That(master.SetKeyInserted(false), Is.True);
            }

            sensor.SetPhysicalSpeedMps(-10f);
            sensor.CollectInput();
            sensor.Calculate(0.5f);
            atc.CollectInput();
            atc.Calculate(0.5f);
            Assert.That(atc.Context.Input.signedSpeedMps, Is.EqualTo(-10f));
            Assert.That(atc.Context.State.position.frontPosition.distanceOnAtcEdgeM, Is.EqualTo(45f));
            Assert.That(atc.Context.State.position.rearPosition.distanceOnAtcEdgeM, Is.EqualTo(15f));
            Assert.That(atc.Context.State.position.isPositionKnown, Is.True);
            Assert.That(atc.Context.State.operation.isAtcEnabled, Is.False);
        }

        [Test]
        public void RebuildingCarEquipmentKeepsSingleTrainAtc()
        {
            Transform common = equipmentBuilder.transform.Find("Common");
            Assert.That(common, Is.Not.Null);
            var tims = new GameObject("Tims");
            tims.transform.SetParent(common, false);
            Assert.That(equipmentBuilder.Build(), Is.True);
            Assert.That(equipmentBuilder.Build(), Is.True);
            Assert.That(common.Find("Tims"), Is.EqualTo(tims.transform));
            Assert.That(atc.transform.parent, Is.EqualTo(common));
            var controllers = train.GetComponentsInChildren<TrainAtcController>(true);
            Assert.That(controllers.Length, Is.EqualTo(1));
            Assert.That(controllers[0], Is.SameAs(atc));
            Assert.That(atc.GetComponent<TrainEquipmentAssignment>(), Is.Null);
        }

        [TestCase(CarType.Tc1, 2)]
        [TestCase(CarType.Tc2, 2)]
        [TestCase(CarType.M, 0)]
        [TestCase(CarType.T, 0)]
        public void ReceiverGenerationRemainsLimitedToTcCars(CarType type, int count)
        {
            car.carType = type;
            Assert.That(simulationBuilder.Build(), Is.True);
            Assert.That(simulationBuilder.Build(), Is.True);
            Assert.That(train.GetComponentsInChildren<TrainAtcReceiverController>(true).Length, Is.EqualTo(count));
        }

        [Test]
        public void CommonEquipmentUsesDefinitionAndReusesExistingObjects()
        {
            var common = train.transform.Find("Common");
            var existing = new GameObject("ExistingEquipment");
            existing.transform.SetParent(common, false);
            var existingPrefab = new GameObject("ExistingEquipment");
            var additionalPrefab = new GameObject("AdditionalEquipment");
            try
            {
                consist.commonEquipmentPrefabs = new[]
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>(AtcPrefabPath),
                    existingPrefab,
                    additionalPrefab,
                    additionalPrefab,
                    null
                };
                Assert.That(equipmentBuilder.Build(), Is.True);
                Assert.That(equipmentBuilder.Build(), Is.True);
                Assert.That(common.childCount, Is.EqualTo(3));
                Assert.That(common.Find("ExistingEquipment"), Is.EqualTo(existing.transform));
                Assert.That(common.Find("AdditionalEquipment"), Is.Not.Null);
                Assert.That(common.GetComponentsInChildren<TrainEquipmentAssignment>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(existingPrefab);
                Object.DestroyImmediate(additionalPrefab);
            }
        }

        [TestCase("Series1000", "Tc1")]
        [TestCase("Series1000", "Tc2")]
        [TestCase("Series10000", "Tc1")]
        [TestCase("Series10000", "Tc2")]
        public void TcDefinitionsIncludeReceiverWithoutAtc(string series, string type)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CarDefinitionAsset>(
                $"Assets/Nakatetsu/Train/{series}/Data/{series}_{type}.asset");
            Assert.That(definition.atcReceiverPrefab, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(ReceiverPrefabPath)));
            Assert.That(definition.additionalEquipmentPrefabs, Has.No.Member(AssetDatabase.LoadAssetAtPath<GameObject>(AtcPrefabPath)));
        }

        private void CreateMaster(int carIndex)
        {
            var masterObject = new GameObject("MasterController");
            masterObject.transform.SetParent(train.transform, false);
            var master = masterObject.AddComponent<MasterController>();
            master.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            master.ConfigureLimits(4, 7, 8);
            Assert.That(master.SetKeyInserted(true), Is.True);
            Assert.That(master.SetReverserPosition(ReverserPosition.Forward), Is.True);
        }

        private TrainTrackPositionController ConfigureInitialTrackPosition(bool frontFacesAtoB)
        {
            car.lengthM = 20f;
            car.bogieCenterDistanceM = 16f;
            var graphObject = new GameObject("TrackGraph");
            graphObject.transform.SetParent(train.transform, false);
            var graph = graphObject.AddComponent<TrackGraphController>();
            var graphFields = new SerializedObject(graph);
            graphFields.FindProperty("graphAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(
                "Assets/Nakatetsu/Track/NtLine/Data/NtLineGraph.asset");
            graphFields.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(graph.TryInitialize(out var error), Is.True, error);

            var position = train.AddComponent<TrainTrackPositionController>();
            position.SetTrackGraphController(graph);
            position.SetTrackPosition("nt-approach", 400f, frontFacesAtoB);
            var simulationFields = new SerializedObject(simulation);
            simulationFields.FindProperty("atcGraphAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TrackAtcGraphAsset>(
                "Assets/Nakatetsu/Track/NtLine/Data/NtLineAtcGraph.asset");
            simulationFields.ApplyModifiedPropertiesWithoutUndo();
            typeof(TrainSimulationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(simulation, null);
            simulation.ResolveReferences();
            return position;
        }

    }
}
