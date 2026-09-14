using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Speed;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Tests
{
    public sealed class TimsSpeedTests
    {
        private const string PrefabPath = "Assets/Nakatetsu/Train/Equipment/SpeedSensor/Prefabs/SpeedSensor.prefab";
        private GameObject train;
        private ConsistDefinitionAsset definition;
        private CarDefinitionAsset car;
        private TimsCommunicationController communication;
        private TrainSimulationController simulation;
        private TrainPhysicsController physics;
        private readonly SpeedSensor[] sensors = new SpeedSensor[2];

        [SetUp]
        public void SetUp()
        {
            train = new GameObject("Train");
            definition = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            car.emptyMassKg = 30000f;
            car.additionalEquipmentPrefabs = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) };
            Assert.That(car.additionalEquipmentPrefabs[0], Is.Not.Null);
            definition.cars.Add(car);
            definition.cars.Add(car);
            TrainRoot root = train.AddComponent<TrainRoot>();
            root.Configure(definition);
            var builder = train.AddComponent<TrainEquipmentBuilder>();
            builder.Configure(root);
            Assert.That(builder.Build(), Is.True);
            for (int i = 0; i < 2; i++)
            {
                sensors[i] = builder.CarEquipments[i].AdditionalEquipments[0].GetComponent<SpeedSensor>();
                Assert.That(sensors[i].GetComponent<SpeedSensorTimsBusSource>(), Is.Not.Null);
            }
            communication = train.AddComponent<TimsCommunicationController>();
            Invoke(communication, "Awake");
            physics = train.AddComponent<TrainPhysicsController>();
            simulation = train.AddComponent<TrainSimulationController>();
            Invoke(simulation, "Awake");
            simulation.ResolveReferences();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(car);
        }

        [TestCase(0f)]
        [TestCase(10f)]
        [TestCase(-10f)]
        public void SimulationPublishesMeasuredSpeedInSameCollection(float speed)
        {
            physics.Context.State.signedVelocityMps = speed;
            physics.ApplyOutput(0f);
            Invoke(simulation, "Step", 0.1f);

            AssertMasterSpeed(Mathf.Abs(speed));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(communication.GetLocalBus(i).TryGetFloat(
                    SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, out float measured), Is.True);
                Assert.That(measured, Is.EqualTo(Mathf.Abs(speed)));
            }
            // 一般Equipment群からも進めると入力を消費済みのため無効になる。
            Assert.That(sensors[0].TryGetMeasuredSpeedMps(out _), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDisabledPhysicsClearsSpeedInsteadOfPublishingZero(bool destroy)
        {
            Invoke(simulation, "Step", 0.1f);
            AssertMasterSpeed(0f);
            if (destroy) Object.DestroyImmediate(physics);
            else physics.enabled = false;
            Invoke(simulation, "Step", 0.1f);
            AssertNoMasterSpeed();
            Assert.That(communication.GetLocalBus(0).TryGetFloat(
                SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, out _), Is.False);
        }

        [Test]
        public void SelectsFirstValidCarAndFallsBackWithoutMixingBuses()
        {
            Measure(sensors[0], 10f);
            Measure(sensors[1], 20f);
            communication.CollectInputSources();
            AssertMasterSpeed(10f);
            sensors[0].enabled = false;
            communication.CollectInputSources();
            AssertMasterSpeed(20f);
            sensors[1].GetComponent<SpeedSensorTimsBusSource>().enabled = false;
            communication.CollectInputSources();
            AssertNoMasterSpeed();
        }

        [Test]
        public void AnotherTrainDoesNotReadThisTrainsMeasurements()
        {
            Invoke(simulation, "Step", 0.1f);
            var other = new GameObject("OtherTrain");
            try
            {
                other.AddComponent<TrainRoot>().Configure(definition);
                var otherCommunication = other.AddComponent<TimsCommunicationController>();
                Invoke(otherCommunication, "Awake");
                otherCommunication.CollectInputSources();
                Assert.That(otherCommunication.MasterBus.TryGetFloat(TimsSpeedController.SpeedMpsKey, out _), Is.False);
                AssertMasterSpeed(0f);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void NeverPublishesUninitializedMeasurement()
        {
            communication.CollectInputSources();
            AssertNoMasterSpeed();
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(float.MaxValue)]
        public void InvalidLocalValueDoesNotReachMasterBus(float value)
        {
            communication.GetLocalBus(0).SetFloat(SpeedSensorTimsBusSource.MeasuredSpeedMpsKey, value);
            communication.GetComponent<TimsSpeedController>().CalculateAndPublish();
            AssertNoMasterSpeed();
        }

        [TestCase("Tc1")]
        [TestCase("Tc2")]
        [TestCase("M")]
        [TestCase("T")]
        public void CarDefinitionsIncludeSensorPrefab(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<CarDefinitionAsset>(
                $"Assets/Nakatetsu/Train/Consist/Definitions/Data/{name}.asset");
            Assert.That(asset.additionalEquipmentPrefabs,
                Does.Contain(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
        }

        private void AssertMasterSpeed(float speed)
        {
            Assert.That(communication.MasterBus.TryGetFloat(TimsSpeedController.SpeedMpsKey, out float mps), Is.True);
            Assert.That(mps, Is.EqualTo(speed));
            Assert.That(communication.MasterBus.TryGetFloat(TimsSpeedController.SpeedKmhKey, out float kmh), Is.True);
            Assert.That(kmh, Is.EqualTo(speed * 3.6f).Within(0.0001f));
            Assert.That(communication.MasterBus.TryGetBool(TimsSpeedController.HasValidSpeedKey, out bool valid), Is.True);
            Assert.That(valid, Is.True);
        }

        private void AssertNoMasterSpeed()
        {
            Assert.That(communication.MasterBus.TryGetFloat(TimsSpeedController.SpeedMpsKey, out _), Is.False);
            Assert.That(communication.MasterBus.TryGetFloat(TimsSpeedController.SpeedKmhKey, out _), Is.False);
            Assert.That(communication.MasterBus.TryGetBool(TimsSpeedController.HasValidSpeedKey, out bool valid), Is.True);
            Assert.That(valid, Is.False);
        }

        private static void Measure(SpeedSensor sensor, float speed)
        {
            sensor.SetPhysicalSpeedMps(speed);
            sensor.CollectInput();
            sensor.Calculate(0.1f);
            sensor.ApplyOutput(0.1f);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }
    }
}
