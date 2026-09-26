using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Traction;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Equipment.Traction.Vvvf;
using Nakatetsu.Train.Presentation.Gauges;
using Nakatetsu.Train.Simulation.Traction.Motor;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsCurrentGaugeTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset car;
        private VvvfDefinitionAsset vvvfDefinition;
        private TrainDriveDefinition driveDefinition;
        private MotorDefinitionAsset motorDefinition;
        private TimsCommunicationController tims;
        private VvvfController first;
        private VvvfController second;
        private TimsCurrentGauge gauge;
        private Image power;
        private Image regen;
        private TMP_Text label;

        [SetUp]
        public void SetUp()
        {
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            for (int i = 0; i < 3; i++) consist.cars.Add(car);
            vvvfDefinition = ScriptableObject.CreateInstance<VvvfDefinitionAsset>();
            driveDefinition = ScriptableObject.CreateInstance<TrainDriveDefinition>();
            motorDefinition = ScriptableObject.CreateInstance<MotorDefinitionAsset>();
            train = new GameObject("Train");
            train.AddComponent<TrainRoot>().Configure(consist);
            tims = train.AddComponent<TimsCommunicationController>();
            typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tims, null);
            // 先頭は付随車。Hierarchy順ではなく車両順で選ぶ。
            second = CreateVvvf(2);
            first = CreateVvvf(1);
            gauge = Child("Gauge", train.transform).AddComponent<TimsCurrentGauge>();
            power = Child("Power", gauge.transform).AddComponent<Image>();
            regen = Child("Regen", gauge.transform).AddComponent<Image>();
            label = Child("Current", gauge.transform).AddComponent<TextMeshProUGUI>();
            SetField(gauge, "powerBar", power);
            SetField(gauge, "regenBar", regen);
            SetField(gauge, "currentLabel", label);
            SetField(gauge, "maximumCurrentA", 2000f);
            SetField(gauge, "height", 250f);
            gauge.SetTimsSource(tims);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(consist);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(vvvfDefinition);
            Object.DestroyImmediate(driveDefinition);
            Object.DestroyImmediate(motorDefinition);
        }

        [TestCase(1000f, 800f, 100f, 0f, "800")]
        [TestCase(-1000f, 800f, 0f, 100f, "-800")]
        [TestCase(0f, 0f, 0f, 0f, "0")]
        [TestCase(1000f, 3000f, 250f, 0f, "3000")]
        [TestCase(-1000f, 3000f, 0f, 250f, "-3000")]
        public void MeasuredCurrentFlowsThroughTimsIntoSeparateBars(
            float force, float current, float powerHeight, float regenHeight, string text)
        {
            first.SetMotorMeasurement(0f, force, current, 0f);
            second.SetMotorMeasurement(0f, 5000f, 1200f, 0f);
            CollectAndRefresh();
            Assert.That(power.enabled, Is.True);
            Assert.That(regen.enabled, Is.True);
            Assert.That(power.rectTransform.sizeDelta.y, Is.EqualTo(powerHeight).Within(0.001f));
            Assert.That(regen.rectTransform.sizeDelta.y, Is.EqualTo(regenHeight).Within(0.001f));
            Assert.That(label.text, Is.EqualTo(text));
            AssertSourceCar(1);
        }

        [Test]
        public void UsesMeasuredForceEvenWhenCommandModeDisagrees()
        {
            first.Output.driveMode = VvvfDriveMode.Power;
            first.SetMotorMeasurement(-100f, -1000f, 600f, -10000f);
            CollectAndRefresh();
            Assert.That(label.text, Is.EqualTo("-600"));
            Assert.That(power.rectTransform.sizeDelta.y, Is.Zero);
            Assert.That(regen.rectTransform.sizeDelta.y, Is.EqualTo(75f));
        }

        [TestCase("equipment")]
        [TestCase("source")]
        [TestCase("removed")]
        [TestCase("unassigned")]
        public void UnavailableRepresentativeFallsBackAndMissingDataClearsDisplay(string reason)
        {
            first.SetMotorMeasurement(0f, 1000f, 800f, 0f);
            second.SetMotorMeasurement(0f, -1000f, 400f, 0f);
            CollectAndRefresh();
            switch (reason)
            {
                case "equipment": first.enabled = false; break;
                case "source": first.GetComponent<TimsTractionBusSource>().enabled = false; break;
                case "removed": Object.DestroyImmediate(first.gameObject); break;
                case "unassigned": first.GetComponent<TrainEquipmentAssignment>().AssignCarIndex(-1); break;
            }
            CollectAndRefresh();
            Assert.That(label.text, Is.EqualTo("-400"));
            AssertSourceCar(2);
            second.gameObject.SetActive(false);
            CollectAndRefresh();
            AssertMissing();
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidMeasurementDoesNotReachDisplay(float invalid)
        {
            first.SetMotorMeasurement(0f, 100f, invalid, 0f);
            second.SetMotorMeasurement(0f, invalid, 100f, 0f);
            CollectAndRefresh();
            AssertMissing();
        }

        [Test]
        public void InvalidReceivedCurrentIsRejectedByTims()
        {
            tims.GetLocalBus(1).SetFloat(TimsTractionBusSource.SignedMotorCurrentAKey, float.NaN);
            tims.GetComponent<TimsCurrentController>().CalculateAndPublish();
            gauge.Refresh();
            AssertMissing();
        }

        [Test]
        public void CurrentUpdatesAtTimsTransferInterval()
        {
            first.SetMotorMeasurement(0f, 1000f, 800f, 0f);
            CollectAndRefresh();
            first.SetMotorMeasurement(0f, -1000f, 400f, 0f);
            tims.CollectInputSources(0.1f);
            gauge.Refresh();
            Assert.That(label.text, Is.EqualTo("800"));
            tims.CollectInputSources(0.15f);
            gauge.Refresh();
            Assert.That(label.text, Is.EqualTo("-400"));
        }

        [Test]
        public void SwitchingToAnotherTrainCannotKeepOriginalTrainsCurrent()
        {
            first.SetMotorMeasurement(0f, 1000f, 800f, 0f);
            CollectAndRefresh();
            var other = new GameObject("OtherTrain");
            try
            {
                other.AddComponent<TrainRoot>().Configure(consist);
                var otherTims = other.AddComponent<TimsCommunicationController>();
                typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(otherTims, null);
                otherTims.CollectInputSources();
                gauge.SetTimsSource(otherTims);
                Assert.That(label.text, Is.EqualTo("--"));
                Assert.That(power.enabled, Is.False);
                gauge.SetTimsSource(tims);
                Assert.That(label.text, Is.EqualTo("800"));
                tims.enabled = false;
                gauge.Refresh();
                Assert.That(label.text, Is.EqualTo("--"));
            }
            finally { Object.DestroyImmediate(other); }
        }

        private VvvfController CreateVvvf(int carIndex)
        {
            var go = Child("VVVF" + carIndex, train.transform);
            go.AddComponent<TrainEquipmentAssignment>().AssignCarIndex(carIndex);
            var controller = go.AddComponent<VvvfController>();
            controller.Configure(vvvfDefinition, driveDefinition);
            controller.ConfigureMotor(motorDefinition, driveDefinition, 4);
            go.AddComponent<TimsTractionBusSource>();
            return controller;
        }

        private void CollectAndRefresh()
        {
            tims.CollectInputSources();
            gauge.Refresh();
        }

        private void AssertSourceCar(int index)
        {
            Assert.That(tims.MasterBus.TryGetInt(TimsCurrentController.CurrentSourceCarIndexKey, out int actual), Is.True);
            Assert.That(actual, Is.EqualTo(index));
        }

        private void AssertMissing()
        {
            Assert.That(tims.MasterBus.TryGetFloat(TimsCurrentController.SignedMotorCurrentAKey, out _), Is.False);
            Assert.That(tims.MasterBus.TryGetInt(TimsCurrentController.CurrentSourceCarIndexKey, out _), Is.False);
            Assert.That(label.text, Is.EqualTo("--"));
            Assert.That(power.enabled, Is.False);
            Assert.That(regen.enabled, Is.False);
            Assert.That(power.rectTransform.sizeDelta.y, Is.Zero);
            Assert.That(regen.rectTransform.sizeDelta.y, Is.Zero);
        }

        private static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
