using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Simulation.Atc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Integration.Tests
{
    public sealed class TrainAtcMassInputTests
    {
        private GameObject train;
        private TrainRoot root;
        private ConsistDefinitionAsset consist;
        private readonly List<CarDefinitionAsset> definitions = new();
        private TimsCommunicationController communication;
        private TrainAtcController atc;
        private TrainAtcMassInputAdapter adapter;
        private TrainAtcReceiverController frontReceiver;
        private TrainAtcReceiverController rearReceiver;

        [SetUp]
        public void SetUp()
        {
            train = new GameObject("Train");
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            AddCar(20f, 30000f);
            AddCar(24f, 32000f);
            AddCar(18f, 28000f);
            root = train.AddComponent<TrainRoot>();
            root.Configure(consist);
            communication = Child("Tims").AddComponent<TimsCommunicationController>();
            typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(communication, null);
            SetMass(0, 36000f);
            SetMass(1, 40000f);
            SetMass(2, 31000f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nakatetsu/Train/Equipment/Atc/Prefabs/TrainAtc.prefab");
            var atcObject = Object.Instantiate(prefab, train.transform);
            atc = atcObject.GetComponent<TrainAtcController>();
            adapter = atcObject.GetComponent<TrainAtcMassInputAdapter>();
            Assert.That(adapter, Is.Not.Null);
            frontReceiver = Child("FrontReceiver").AddComponent<TrainAtcReceiverController>();
            rearReceiver = Child("RearReceiver").AddComponent<TrainAtcReceiverController>();
            atc.SetReceivers(frontReceiver, rearReceiver);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(consist);
            foreach (var definition in definitions)
            {
                Object.DestroyImmediate(definition);
            }
            definitions.Clear();
        }

        [Test]
        public void CollectsMeasuredTotalMassWithoutAddingEmptyMassTwice()
        {
            var cars = new List<TrainAtcCarInput>();
            Assert.That(adapter.TryReadCarInputs(cars), Is.True);
            Assert.That(cars.Count, Is.EqualTo(3));
            Assert.That(cars[0].massKg, Is.EqualTo(36000f));
            Assert.That(cars[1].massKg, Is.EqualTo(40000f));
            Assert.That(cars[2].massKg, Is.EqualTo(31000f));
            Assert.That(cars[0].centerDistanceFromFrontM, Is.EqualTo(10f));
            Assert.That(cars[1].centerDistanceFromFrontM, Is.EqualTo(32f));
            Assert.That(cars[2].centerDistanceFromFrontM, Is.EqualTo(53f));
        }

        [Test]
        public void MeasurementBelowEmptyMassDoesNotCreateNegativeLoad()
        {
            SetMass(0, 20000f);
            var cars = new List<TrainAtcCarInput>();
            Assert.That(adapter.TryReadCarInputs(cars), Is.True);
            Assert.That(cars[0].massKg, Is.EqualTo(30000f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MissingCarMeasurementClearsPreviousAndPartialResults(int carIndex)
        {
            var cars = new List<TrainAtcCarInput>();
            Assert.That(adapter.TryReadCarInputs(cars), Is.True);
            communication.GetLocalBus(carIndex).Remove(BrakeControlDeviceTimsBusSource.MassKgKey);
            Assert.That(adapter.TryReadCarInputs(cars), Is.False);
            Assert.That(cars, Is.Empty);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidMassAfterValidFirstCarDoesNotReturnPartialResults(float measuredMassKg)
        {
            var cars = new List<TrainAtcCarInput>();
            Assert.That(adapter.TryReadCarInputs(cars), Is.True);
            SetMass(1, measuredMassKg);
            Assert.That(adapter.TryReadCarInputs(cars), Is.False);
            Assert.That(cars, Is.Empty);
        }

        [TestCase("missingDefinition")]
        [TestCase("zeroLength")]
        [TestCase("invalidLength")]
        [TestCase("zeroEmptyMass")]
        [TestCase("invalidEmptyMass")]
        public void InvalidCarDefinitionClearsPreviousAndPartialResults(string condition)
        {
            var cars = new List<TrainAtcCarInput>();
            Assert.That(adapter.TryReadCarInputs(cars), Is.True);
            switch (condition)
            {
                case "missingDefinition": consist.cars[1] = null; break;
                case "zeroLength": definitions[1].lengthM = 0f; break;
                case "invalidLength": definitions[1].lengthM = float.PositiveInfinity; break;
                case "zeroEmptyMass": definitions[1].emptyMassKg = 0f; break;
                case "invalidEmptyMass": definitions[1].emptyMassKg = float.NaN; break;
            }
            Assert.That(adapter.TryReadCarInputs(cars), Is.False);
            Assert.That(cars, Is.Empty);
        }

        [Test]
        public void CollectsMountedReceiverDistancesAndKeepsInputSnapshotUntilNextCollection()
        {
            SetOffset(frontReceiver, 2f);
            SetOffset(rearReceiver, -3f);
            atc.CollectInput();
            Assert.That(atc.Context.Input.hasCarMasses, Is.True);
            Assert.That(atc.Context.Input.frontReceiverDistanceFromFrontM, Is.EqualTo(8f));
            Assert.That(atc.Context.Input.rearReceiverDistanceFromFrontM, Is.EqualTo(56f));
            Assert.That(atc.Context.Input.cars[1].massKg, Is.EqualTo(40000f));

            SetMass(1, 42000f);
            SetOffset(frontReceiver, 4f);
            Assert.That(atc.Context.Input.cars[1].massKg, Is.EqualTo(40000f));
            Assert.That(atc.Context.Input.frontReceiverDistanceFromFrontM, Is.EqualTo(8f));
            atc.CollectInput();
            Assert.That(atc.Context.Input.cars[1].massKg, Is.EqualTo(42000f));
            Assert.That(atc.Context.Input.frontReceiverDistanceFromFrontM, Is.EqualTo(6f));
        }

        [TestCase("adapter")]
        [TestCase("tims")]
        [TestCase("root")]
        [TestCase("atc")]
        [TestCase("train")]
        [TestCase("missingMass")]
        public void UnavailableMassInputClearsCollectedCarsAndReceiverDistances(string condition)
        {
            SetOffset(frontReceiver, 2f);
            SetOffset(rearReceiver, -3f);
            atc.CollectInput();
            Assert.That(atc.Context.Input.hasCarMasses, Is.True);
            switch (condition)
            {
                case "adapter": adapter.enabled = false; break;
                case "tims": communication.enabled = false; break;
                case "root": root.enabled = false; break;
                case "atc": atc.enabled = false; break;
                case "train": train.SetActive(false); break;
                case "missingMass": communication.GetLocalBus(1).Remove(BrakeControlDeviceTimsBusSource.MassKgKey); break;
            }
            atc.CollectInput();
            AssertClearedMassInput();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingReceiverDoesNotLeavePreviousMassInputs(bool missingFront)
        {
            atc.CollectInput();
            Assert.That(atc.Context.Input.hasCarMasses, Is.True);
            if (missingFront)
            {
                atc.SetReceivers(null, rearReceiver);
            }
            else
            {
                atc.SetReceivers(frontReceiver, null);
            }
            atc.CollectInput();
            AssertClearedMassInput();
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidReceiverOffsetIsCapturedAndRejectedDuringValidation(float offsetM)
        {
            atc.CollectInput();
            Assert.That(atc.Context.Input.hasCarMasses, Is.True);
            SetOffset(frontReceiver, offsetM);
            atc.CollectInput();

            // 収集では測定値をそのまま保持する。計算に使えるかは工程4で判定する。
            Assert.That(atc.Context.Input.hasCarMasses, Is.True);
            Assert.That(atc.Context.Input.cars.Count, Is.EqualTo(3));
            float receiverDistanceM = atc.Context.Input.frontReceiverDistanceFromFrontM;
            Assert.That(float.IsNaN(receiverDistanceM) || float.IsInfinity(receiverDistanceM), Is.True);

            atc.Calculate(0.01f);
            Assert.That(atc.Context.State.validation.isInputValid, Is.False);
            Assert.That(atc.Context.State.isAtcHealthy, Is.False);
        }

        private void AssertClearedMassInput()
        {
            Assert.That(atc.Context.Input.hasCarMasses, Is.False);
            Assert.That(atc.Context.Input.cars, Is.Empty);
            Assert.That(atc.Context.Input.frontReceiverDistanceFromFrontM, Is.Zero);
            Assert.That(atc.Context.Input.rearReceiverDistanceFromFrontM, Is.Zero);
        }

        private void AddCar(float lengthM, float emptyMassKg)
        {
            var definition = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            definition.lengthM = lengthM;
            definition.emptyMassKg = emptyMassKg;
            definitions.Add(definition);
            consist.cars.Add(definition);
        }

        private void SetMass(int carIndex, float massKg)
        {
            communication.GetLocalBus(carIndex).SetFloat(BrakeControlDeviceTimsBusSource.MassKgKey, massKg);
        }

        private static void SetOffset(TrainAtcReceiverController receiver, float offsetM)
        {
            var serialized = new SerializedObject(receiver);
            serialized.FindProperty("offsetFromCarCenterM").floatValue = offsetM;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(train.transform, false);
            return child;
        }
    }
}
