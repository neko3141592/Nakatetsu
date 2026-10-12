using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.BrakeStatus;
using Nakatetsu.Train.Equipment.Tims.Presentation.TrainStatus;
using Nakatetsu.Train.Equipment.Tims.Traction;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsBrakeStatusDisplayTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset motor, trailer;
        private TimsCommunicationController tims;
        private TimsTrainStatusDisplay trainStatus;
        private TimsBrakeStatusDisplay display;
        private RectTransform canvas, bcPlot, regenPlot;
        private GameObject sample, staticLabel;

        [SetUp]
        public void SetUp()
        {
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            motor = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            motor.carType = CarType.M;
            motor.motorCount = 4;
            trailer = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            trailer.carType = CarType.T;
            for (int i = 0; i < 4; i++) consist.cars.Add(i == 3 ? trailer : motor);
            train = new GameObject("BrakeStatusTest");
            train.AddComponent<TrainRoot>().Configure(consist);
            tims = train.AddComponent<TimsCommunicationController>();
            InvokeAwake(tims);
            canvas = CreateRect("Canvas", train.transform, new Vector2(1536f, 1024f));
            var statusRect = CreateRect("TrainStatus", canvas, new Vector2(1000f, 200f));
            statusRect.anchoredPosition = new Vector2(40f, 320f);
            trainStatus = statusRect.gameObject.AddComponent<TimsTrainStatusDisplay>();
            SetTrainStatus("centerPosition", new Vector2(22f, -30f));
            SetTrainStatus("spacingX", 100f);
            bcPlot = CreateRect("BCPlot", canvas, new Vector2(1000f, 160f));
            bcPlot.anchoredPosition = new Vector2(-45f, 100f);
            regenPlot = CreateRect("RegenPlot", canvas, new Vector2(1000f, 200f));
            regenPlot.anchoredPosition = new Vector2(65f, -150f);
            var displayRect = CreateRect("BrakeStatus", canvas, Vector2.zero);
            display = displayRect.gameObject.AddComponent<TimsBrakeStatusDisplay>();
            sample = new GameObject("Sample");
            sample.transform.SetParent(display.transform);
            staticLabel = new GameObject("StaticLabel");
            staticLabel.transform.SetParent(bcPlot);
            Set("trainStatus", trainStatus);
            Set("bcPlot", bcPlot);
            Set("regenPlot", regenPlot);
            Set("editorSample", sample);
            display.SetTimsSource(tims);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train);
            Object.DestroyImmediate(consist);
            Object.DestroyImmediate(motor);
            Object.DestroyImmediate(trailer);
        }

        [Test]
        public void ReadsEachCarsMeasuredPressureAndRegenerationInTheirOwnUnits()
        {
            SetCarReadings(0, 200f, 50000f);
            SetCarReadings(1, 400f, 25000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 75000f, 40000f, 0f, 0f });
            display.Refresh();

            Assert.That(Bar("BC", 0).rectTransform.rect.height, Is.EqualTo(40f));
            Assert.That(Bar("BC", 1).rectTransform.rect.height, Is.EqualTo(80f));
            Assert.That(Bar("PTN", 0).rectTransform.rect.height, Is.EqualTo(150f));
            Assert.That(Bar("FB", 0).rectTransform.rect.height, Is.EqualTo(100f));
            Assert.That(Bar("PTN", 1).rectTransform.rect.height, Is.EqualTo(80f));
            Assert.That(Bar("FB", 1).rectTransform.rect.height, Is.EqualTo(50f));
            Assert.That(Bar("BC", 0).color, Is.EqualTo((Color)new Color32(238, 174, 0, 255)));
            Assert.That(Bar("PTN", 0).color, Is.EqualTo((Color)new Color32(238, 174, 0, 255)));
            Assert.That(Bar("FB", 0).color, Is.EqualTo((Color)new Color32(48, 156, 255, 255)));
            Assert.That(ValueLabel("BC", 0).text, Is.EqualTo("200"));
            Assert.That(ValueLabel("PTN", 0).text, Is.EqualTo("75"));
            Assert.That(ValueLabel("FB", 0).text, Is.EqualTo("50"));
            Assert.That(ValueLabel("BC", 0).color, Is.EqualTo(Bar("BC", 0).color));
            Assert.That(ValueLabel("PTN", 0).color, Is.EqualTo(Bar("PTN", 0).color));
            Assert.That(ValueLabel("FB", 0).color, Is.EqualTo(Bar("FB", 0).color));
        }

        [TestCase(0f, "0", "0")]
        [TestCase(200.5f, "201", "201")]
        [TestCase(12.34f, "12", "12")]
        [TestCase(12.36f, "12", "12")]
        [TestCase(float.NaN, "--", "--")]
        [TestCase(float.PositiveInfinity, "--", "--")]
        [TestCase(-1f, "--", "--")]
        public void ValueFormattersUseIntegersForKPaAndKN(float value,
            string pressure, string force)
        {
            Assert.That(TimsBrakeStatusDisplay.FormatPressureKPa(value), Is.EqualTo(pressure));
            Assert.That(TimsBrakeStatusDisplay.FormatRegenKN(value), Is.EqualTo(force));
        }

        [Test]
        public void ValueLabelsUseCarCentersAndTwoRowsBelowTheRegenerationBaseline()
        {
            SetCarReadings(0, 201.5f, 12345f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 67890f, 0f, 0f, 0f });
            display.Refresh();
            Assert.That(ValueLabel("BC", 0).text, Is.EqualTo("202"));
            Assert.That(ValueLabel("PTN", 0).text, Is.EqualTo("68"));
            Assert.That(ValueLabel("FB", 0).text, Is.EqualTo("12"));

            trainStatus.TryGetCarCenter(0, bcPlot, out Vector2 bcCenter);
            trainStatus.TryGetCarCenter(0, regenPlot, out Vector2 regenCenter);
            Assert.That(ValueLabel("BC", 0).rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(bcCenter.x, bcPlot.rect.yMin - 8f)));
            Assert.That(ValueLabel("PTN", 0).rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(regenCenter.x, regenPlot.rect.yMin - 8f)));
            Assert.That(ValueLabel("FB", 0).rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(regenCenter.x, regenPlot.rect.yMin - 30f)));
            foreach (string reading in new[] { "BC", "PTN", "FB" })
            {
                TMP_Text label = ValueLabel(reading, 0);
                Assert.That(label.rectTransform.rect.width, Is.EqualTo(70f));
                Assert.That(label.rectTransform.pivot, Is.EqualTo(new Vector2(.5f, 1f)));
                Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                Assert.That(label.fontSize, Is.EqualTo(reading == "BC" ? 20f : 18f));
            }
            Assert.That(ValueLabel("PTN", 0).rectTransform.anchoredPosition.y -
                ValueLabel("FB", 0).rectTransform.anchoredPosition.y, Is.EqualTo(22f));
            Assert.That(display.GeneratedBcRoot.Find("BC_01_Missing"), Is.Null);
            Assert.That(display.GeneratedRegenRoot.Find("PTN_01_Missing"), Is.Null);
        }

        [Test]
        public void BarsFollowTheTrainStatusCentersAcrossDifferentPlotTransforms()
        {
            trainStatus.transform.localScale = new Vector3(1.2f, 1f, 1f);
            regenPlot.localScale = new Vector3(.8f, 1f, 1f);
            bcPlot.pivot = new Vector2(.2f, .3f);
            SetCarReadings(0, 200f, 50000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 75000f, 0f, 0f, 0f });
            display.Refresh();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(trainStatus.TryGetCarCenter(i, bcPlot, out Vector2 bcCenter), Is.True);
                Assert.That(trainStatus.TryGetCarCenter(i, regenPlot, out Vector2 regenCenter), Is.True);
                Assert.That(Bar("BC", i).rectTransform.anchoredPosition.x,
                    Is.EqualTo(bcCenter.x).Within(.001f));
                if (consist.cars[i].motorCount == 0)
                {
                    Assert.That(Bar("PTN", i).gameObject.activeSelf, Is.False);
                    Assert.That(Bar("FB", i).gameObject.activeSelf, Is.False);
                    continue;
                }
                Assert.That(Bar("PTN", i).rectTransform.anchoredPosition.x,
                    Is.EqualTo(regenCenter.x - 14f).Within(.001f));
                Assert.That(Bar("FB", i).rectTransform.anchoredPosition.x,
                    Is.EqualTo(regenCenter.x + 14f).Within(.001f));
                Assert.That(Bar("BC", i).rectTransform.anchoredPosition.y, Is.EqualTo(bcPlot.rect.yMin));
                Assert.That(Bar("PTN", i).rectTransform.anchoredPosition.y, Is.EqualTo(regenPlot.rect.yMin));
                Assert.That(Bar("FB", i).rectTransform.anchoredPosition.y, Is.EqualTo(regenPlot.rect.yMin));
                Vector3 actualCenter = Bar("BC", i).rectTransform.TransformPoint(Vector3.zero);
                Assert.That(actualCenter.x, Is.EqualTo(bcPlot.TransformPoint(bcCenter).x).Within(.001f));
            }
            SetTrainStatus("spacingX", 120f);
            SetTrainStatus("centerPosition", new Vector2(60f, 0f));
            display.Refresh();
            trainStatus.TryGetCarCenter(1, regenPlot, out Vector2 movedCenter);
            Assert.That(Bar("PTN", 1).rectTransform.anchoredPosition.x,
                Is.EqualTo(movedCenter.x - 14f).Within(.001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidReadingsClearPreviousBarsAndShowMissingWithoutChangingBus(float invalid)
        {
            SetCarReadings(0, 200f, 50000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 75000f, 0f, 0f, 0f });
            display.Refresh();
            Assert.That(Bar("BC", 0).enabled, Is.True);
            SetCarReadings(0, invalid, invalid);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { invalid, 0f, 0f, 0f });
            display.Refresh();
            foreach (string reading in new[] { "BC", "PTN", "FB" }) AssertMissing(reading, 0);
            Assert.That(tims.GetLocalBus(0).TryGetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey,
                out float pressure), Is.True);
            if (float.IsNaN(invalid)) Assert.That(float.IsNaN(pressure), Is.True);
            else Assert.That(pressure, Is.EqualTo(invalid));
        }

        [Test]
        public void MissingWrongTypeAndUnavailableSignalsNeverLeavePreviousReadingsVisible()
        {
            SetCarReadings(0, 200f, 50000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 75000f, 0f, 0f, 0f });
            display.Refresh();
            TimsBusState local = tims.GetLocalBus(0);
            local.SetBool(BrakeControlDeviceTimsBusSource.PressureKPaKey, true);
            local.Remove(TimsTractionBusSource.ActualRegenForceNKey);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey, new float[0]);
            display.Refresh();
            foreach (string reading in new[] { "BC", "PTN", "FB" }) AssertMissing(reading, 0);

            SetCarReadings(0, 100f, 20000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 10000f, 0f, 0f, 0f });
            local.SetBool(TimsTractionBusSource.IsAvailableKey, false);
            display.Refresh();
            Assert.That(Bar("BC", 0).enabled, Is.True);
            AssertMissing("PTN", 0);
            AssertMissing("FB", 0);
            local.Remove(TimsTractionBusSource.IsAvailableKey);
            display.Refresh();
            AssertMissing("PTN", 0);
            AssertMissing("FB", 0);

            tims.enabled = false;
            display.Refresh();
            foreach (string reading in new[] { "BC", "PTN", "FB" }) AssertMissing(reading, 0);
        }

        [Test]
        public void TrailerDoesNotShowRegenerationAndZeroMeasurementsRemainAvailable()
        {
            SetCarReadings(0, 0f, 0f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey, new[] { 0f });
            display.Refresh();
            foreach (string reading in new[] { "BC", "PTN", "FB" })
            {
                Assert.That(Bar(reading, 0).enabled, Is.True);
                Assert.That(Bar(reading, 0).rectTransform.rect.height, Is.Zero);
                Assert.That(ValueLabel(reading, 0).text, Is.EqualTo("0"));
                Assert.That(ValueLabel(reading, 0).enabled, Is.True);
            }
            foreach (string reading in new[] { "PTN", "FB" })
            {
                Assert.That(Bar(reading, 3).gameObject.activeSelf, Is.False);
                Assert.That(ValueLabel(reading, 3).gameObject.activeSelf, Is.False);
            }
            AssertMissing("BC", 3);
            // 電動車は付随車と違い、指令配列が短いと欠測を明示する。
            AssertMissing("PTN", 1);
            AssertMissing("FB", 1);
        }

        [Test]
        public void ValuesClampToPlotHeightAndFollowPlotResize()
        {
            SetCarReadings(0, 1600f, 200000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 200000f, 0f, 0f, 0f });
            display.Refresh();
            Assert.That(Bar("BC", 0).rectTransform.rect.height, Is.EqualTo(160f));
            Assert.That(Bar("PTN", 0).rectTransform.rect.height, Is.EqualTo(200f));
            Assert.That(Bar("FB", 0).rectTransform.rect.height, Is.EqualTo(200f));
            bcPlot.sizeDelta = new Vector2(1000f, 120f);
            regenPlot.sizeDelta = new Vector2(1000f, 240f);
            display.Refresh();
            Assert.That(Bar("BC", 0).rectTransform.rect.height, Is.EqualTo(120f));
            Assert.That(Bar("FB", 0).rectTransform.rect.height, Is.EqualTo(240f));
        }

        [Test]
        public void FormationChangesRebuildOnlyGeneratedBarsAndHideMissingCarDefinitions()
        {
            Assert.That(display.GeneratedCarCount, Is.EqualTo(4));
            Assert.That(sample.activeSelf, Is.False);
            Assert.That(staticLabel.activeSelf, Is.True);
            consist.cars[1] = null;
            display.Refresh();
            foreach (string reading in new[] { "BC", "PTN", "FB" })
            {
                Assert.That(Bar(reading, 1).gameObject.activeSelf, Is.False);
                Assert.That(ValueLabel(reading, 1).gameObject.activeSelf, Is.False);
            }
            consist.cars.RemoveRange(2, 2);
            display.Refresh();
            Assert.That(display.GeneratedCarCount, Is.EqualTo(2));
            Assert.That(display.GeneratedBcRoot.childCount, Is.EqualTo(4));
            Assert.That(display.GeneratedRegenRoot.childCount, Is.EqualTo(8));
            Assert.That(staticLabel != null && sample != null, Is.True);
            consist.cars.Clear();
            display.Refresh();
            Assert.That(display.GeneratedCarCount, Is.Zero);
            Assert.That(display.GeneratedBcRoot == null, Is.True);
            Assert.That(display.GeneratedRegenRoot == null, Is.True);
            Assert.That(staticLabel != null && sample != null, Is.True);
        }

        [Test]
        public void RebindingUsesTheOtherTrainWithoutRetainingPreviousMeasurements()
        {
            SetCarReadings(0, 200f, 50000f);
            tims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                new[] { 75000f, 0f, 0f, 0f });
            display.Refresh();
            var other = new GameObject("OtherTrain");
            var otherConsist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            try
            {
                otherConsist.cars.Add(motor);
                otherConsist.cars.Add(trailer);
                other.AddComponent<TrainRoot>().Configure(otherConsist);
                var otherTims = other.AddComponent<TimsCommunicationController>();
                InvokeAwake(otherTims);
                display.SetTimsSource(otherTims);
                Assert.That(display.GeneratedCarCount, Is.EqualTo(2));
                foreach (string reading in new[] { "BC", "PTN", "FB" }) AssertMissing(reading, 0);
                otherTims.GetLocalBus(0).SetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey, 400f);
                otherTims.GetLocalBus(0).SetBool(TimsTractionBusSource.IsAvailableKey, true);
                otherTims.GetLocalBus(0).SetFloat(TimsTractionBusSource.ActualRegenForceNKey, 25000f);
                otherTims.MasterBus.SetFloatArray(TimsBrakeController.TargetRegenForcesNKey,
                    new[] { 50000f, 0f });
                display.Refresh();
                Assert.That(Bar("BC", 0).rectTransform.rect.height, Is.EqualTo(80f));
                Assert.That(Bar("PTN", 0).rectTransform.rect.height, Is.EqualTo(100f));
                Assert.That(Bar("FB", 0).rectTransform.rect.height, Is.EqualTo(50f));
                display.SetTimsSource(tims);
                Assert.That(display.GeneratedCarCount, Is.EqualTo(4));
                Assert.That(Bar("BC", 0).rectTransform.rect.height, Is.EqualTo(40f));
                Assert.That(Bar("FB", 0).rectTransform.rect.height, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(otherConsist);
            }
        }

        private void SetCarReadings(int index, float pressureKPa, float feedbackN)
        {
            var local = tims.GetLocalBus(index);
            local.SetFloat(BrakeControlDeviceTimsBusSource.PressureKPaKey, pressureKPa);
            local.SetBool(TimsTractionBusSource.IsAvailableKey, true);
            local.SetFloat(TimsTractionBusSource.ActualRegenForceNKey, feedbackN);
        }

        private Image Bar(string reading, int index) => ReadingRoot(reading)
            .Find($"{reading}_{index + 1:00}").GetComponent<Image>();

        private TMP_Text ValueLabel(string reading, int index) => ReadingRoot(reading)
            .Find($"{reading}_{index + 1:00}_Value").GetComponent<TMP_Text>();

        private RectTransform ReadingRoot(string reading) => reading == "BC"
            ? display.GeneratedBcRoot : display.GeneratedRegenRoot;

        private void AssertMissing(string reading, int index)
        {
            Assert.That(Bar(reading, index).enabled, Is.False, reading);
            Assert.That(Bar(reading, index).rectTransform.rect.height, Is.Zero, reading);
            Assert.That(ValueLabel(reading, index).enabled, Is.True, reading);
            Assert.That(ValueLabel(reading, index).text, Is.EqualTo("--"), reading);
            Assert.That(ValueLabel(reading, index).color, Is.EqualTo((Color)new Color32(105, 110, 127, 255)), reading);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            return rect;
        }

        private static void InvokeAwake(TimsCommunicationController source) => typeof(TimsCommunicationController)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(source, null);

        private void Set(string field, object value) => typeof(TimsBrakeStatusDisplay)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(display, value);

        private void SetTrainStatus(string field, object value) => typeof(TimsTrainStatusDisplay)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(trainStatus, value);
    }
}
