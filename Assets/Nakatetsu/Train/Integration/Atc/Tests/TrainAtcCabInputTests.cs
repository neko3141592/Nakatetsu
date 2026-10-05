using System.Reflection;
using Nakatetsu.Track.Atc;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Simulation.Orchestration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Integration.Tests
{
    public sealed class TrainAtcCabInputTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private TimsDirectionController direction;
        private MasterController front;
        private MasterController rear;
        private TrainAtcController atc;

        [SetUp]
        public void SetUp()
        {
            train = new GameObject("Train");
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            consist.cars.Add(null);
            consist.cars.Add(null);
            train.AddComponent<TrainRoot>().Configure(consist);
            direction = Child("Tims").AddComponent<TimsDirectionController>();
            direction.Output.activatedCabPosition = ActivatedCabPosition.Front;
            front = CreateMaster(0);
            rear = CreateMaster(1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nakatetsu/Train/Equipment/Atc/Prefabs/TrainAtc.prefab");
            var atcObject = Object.Instantiate(prefab, train.transform);
            atc = atcObject.GetComponent<TrainAtcController>();
            Assert.That(atcObject.GetComponent<TrainAtcCabInputAdapter>(), Is.Not.Null);
            var graph = new TrackAtcGraphDefinition();
            graph.atcEdge.Add(new TrackAtcGraphEdge { atcEdgeId = "track", lengthM = 100f });
            Assert.That(atc.TryInitializePosition(graph,
                new TrainAtcPosition { atcEdgeId = "track", distanceOnAtcEdgeM = 40f, frontFacesAtoB = true },
                new TrainAtcPosition { atcEdgeId = "track", distanceOnAtcEdgeM = 20f, frontFacesAtoB = true }), Is.True);
            var sensor = Child("SpeedSensor").AddComponent<SpeedSensor>();
            sensor.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(0);
            sensor.SetPhysicalSpeedMps(0f);
            sensor.CollectInput();
            sensor.Calculate(0.01f);
            atc.SetSpeedSensor(sensor);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(consist);
        }

        [TestCase(ActivatedCabPosition.Front, 0)]
        [TestCase(ActivatedCabPosition.Rear, 1)]
        public void CapturesOnlySelectedCabsKeyAndControlPositions(ActivatedCabPosition cab, int carIndex)
        {
            direction.Output.activatedCabPosition = cab;
            MasterController master = front;
            if (cab == ActivatedCabPosition.Rear)
            {
                master = rear;
            }
            Assert.That(master.SetKeyInserted(true), Is.True);
            Assert.That(master.SetReverserPosition(ReverserPosition.Reverse), Is.True);
            master.SetPowerPosition(3);
            atc.CollectInput();

            Assert.That(atc.Context.Input.hasCabState, Is.True);
            TrainAtcCabInput input = atc.Context.Input.cab;
            Assert.That(input.carIndex, Is.EqualTo(carIndex));
            Assert.That(input.isFrontCab, Is.EqualTo(cab == ActivatedCabPosition.Front));
            Assert.That(input.isKeyInserted, Is.True);
            Assert.That(input.isInputEnabled, Is.True);
            Assert.That(input.powerPosition, Is.EqualTo(3));
            Assert.That(input.brakePosition, Is.Zero);
            Assert.That(input.serviceBrakePosition, Is.Zero);
            Assert.That(input.reverserPosition, Is.EqualTo(ReverserPosition.Reverse));
            Assert.That(input.isNeutral, Is.False);
            Assert.That(input.isEmergencyBrake, Is.False);

            // マスコン操作は次の収集まで、ATCのキャプチャ済みInputを変更しない。
            master.SetNeutral();
            Assert.That(atc.Context.Input.cab.powerPosition, Is.EqualTo(3));
            atc.Calculate(0.01f);
            Assert.That(atc.Context.State.cab, Is.EqualTo(input));
            atc.CollectInput();
            Assert.That(atc.Context.Input.cab.isNeutral, Is.True);

            master.SetBrakePosition(5);
            atc.CollectInput();
            Assert.That(atc.Context.Input.cab.brakePosition, Is.EqualTo(5));
            Assert.That(atc.Context.Input.cab.serviceBrakePosition, Is.EqualTo(5));

            master.SetEmergencyBrake();
            Assert.That(master.SetReverserPosition(ReverserPosition.Neutral), Is.True);
            Assert.That(master.SetKeyInserted(false), Is.True);
            master.SetInputEnabled(false);
            atc.CollectInput();
            Assert.That(atc.Context.Input.hasCabState, Is.True);
            Assert.That(atc.Context.Input.cab.isKeyInserted, Is.False);
            Assert.That(atc.Context.Input.cab.isInputEnabled, Is.False);
            Assert.That(atc.Context.Input.cab.brakePosition, Is.EqualTo(8));
            Assert.That(atc.Context.Input.cab.serviceBrakePosition, Is.EqualTo(7));
            Assert.That(atc.Context.Input.cab.reverserPosition, Is.EqualTo(ReverserPosition.Neutral));
            Assert.That(atc.Context.Input.cab.isEmergencyBrake, Is.True);
        }

        [Test]
        public void CabChangeAndMasterReplacementAreCapturedOnNextCollection()
        {
            Assert.That(front.SetKeyInserted(true), Is.True);
            atc.CollectInput();
            Assert.That(atc.Context.Input.cab.carIndex, Is.Zero);
            Assert.That(atc.Context.Input.cab.isKeyInserted, Is.True);
            direction.Output.activatedCabPosition = ActivatedCabPosition.Rear;
            atc.CollectInput();
            Assert.That(atc.Context.Input.cab.carIndex, Is.EqualTo(1));
            Assert.That(atc.Context.Input.cab.isKeyInserted, Is.False);

            Object.DestroyImmediate(rear.gameObject);
            rear = CreateMaster(1);
            Assert.That(rear.SetKeyInserted(true), Is.True);
            Assert.That(rear.SetReverserPosition(ReverserPosition.Forward), Is.True);
            rear.SetPowerPosition(2);
            atc.CollectInput();
            Assert.That(atc.Context.Input.cab.powerPosition, Is.EqualTo(2));
        }

        [TestCase("noCab")]
        [TestCase("invalidCab")]
        [TestCase("missingMaster")]
        [TestCase("disabledMaster")]
        [TestCase("duplicateMaster")]
        [TestCase("disabledDirection")]
        [TestCase("disabledAdapter")]
        [TestCase("disabledAtc")]
        [TestCase("disabledTrain")]
        public void UnavailableCabClearsPreviousInputAndState(string condition)
        {
            Assert.That(front.SetKeyInserted(true), Is.True);
            atc.CollectInput();
            atc.Calculate(0.01f);
            Assert.That(atc.Context.State.hasCabState, Is.True);
            Assert.That(atc.Context.State.isHealthy, Is.True);

            switch (condition)
            {
                case "noCab": direction.Output.activatedCabPosition = ActivatedCabPosition.None; break;
                case "invalidCab": direction.Output.activatedCabPosition = (ActivatedCabPosition)2; break;
                case "missingMaster": Object.DestroyImmediate(front.gameObject); break;
                case "disabledMaster": front.enabled = false; break;
                case "duplicateMaster": CreateMaster(0); break;
                case "disabledDirection": direction.enabled = false; break;
                case "disabledAdapter": atc.GetComponent<TrainAtcCabInputAdapter>().enabled = false; break;
                case "disabledAtc": atc.enabled = false; break;
                case "disabledTrain": train.GetComponent<TrainRoot>().enabled = false; break;
            }

            atc.CollectInput();
            atc.Calculate(0.01f);
            Assert.That(atc.Context.Input.hasCabState, Is.False);
            Assert.That(atc.Context.Input.cab, Is.EqualTo(default(TrainAtcCabInput)));
            Assert.That(atc.Context.State.hasCabState, Is.False);
            Assert.That(atc.Context.State.cab, Is.EqualTo(default(TrainAtcCabInput)));
            Assert.That(atc.Context.State.isHealthy, Is.False);
            Assert.That(atc.Context.State.isAtcEnabled, Is.False);
            Assert.That(atc.Context.State.currentTelegram, Is.Null);
        }

        [Test]
        public void SimulationCollectsCabStateWithEquipmentInput()
        {
            var simulation = train.AddComponent<TrainSimulationController>();
            typeof(TrainSimulationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(simulation, null);
            simulation.ResolveReferences();
            Assert.That(front.SetKeyInserted(true), Is.True);
            simulation.Calculate(0f);
            Assert.That(atc.Context.Input.hasCabState, Is.True);
            Assert.That(atc.Context.State.cab.isKeyInserted, Is.True);
            Assert.That(atc.Context.State.isAtcEnabled, Is.True);
            Assert.That(front.SetKeyInserted(false), Is.True);
            simulation.Calculate(0f);
            Assert.That(atc.Context.State.cab.isKeyInserted, Is.False);
            Assert.That(atc.Context.State.isHealthy, Is.True);
            Assert.That(atc.Context.State.isAtcEnabled, Is.False);
        }

        private MasterController CreateMaster(int carIndex)
        {
            var master = Child("MasterController").AddComponent<MasterController>();
            master.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            master.ConfigureLimits(4, 7, 8);
            return master;
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(train.transform, false);
            return child;
        }
    }
}
