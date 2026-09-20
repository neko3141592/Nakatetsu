using System.Linq;
using Nakatetsu.Train.Equipment.Tims;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Notch;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsNotchDisplayTests
    {
        private GameObject sourceObject;
        private GameObject displayObject;
        private TimsCommunicationController source;
        private TimsSettingsAsset settings;
        private TimsNotchDisplay display;

        [SetUp]
        public void SetUp()
        {
            sourceObject = new GameObject("TIMS");
            source = sourceObject.AddComponent<TimsCommunicationController>();
            settings = ScriptableObject.CreateInstance<TimsSettingsAsset>();
            sourceObject.AddComponent<TimsRoot>().Configure(settings);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nakatetsu/Train/Equipment/Tims/Display/Notch/Prefabs/TimsNotchDisplay.prefab");
            Assert.That(prefab, Is.Not.Null);
            displayObject = Object.Instantiate(prefab);
            display = displayObject.GetComponent<TimsNotchDisplay>();
            display.Configure(source);
            // EditModeではAwakeが走らないため、実行時のセル生成を明示する。
            if (displayObject.GetComponentInChildren<PowerNotchCell>() == null)
                typeof(TimsNotchDisplay).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(display, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(displayObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(settings);
        }

        private void Publish(int power, int brakeStep, bool emergency = false)
        {
            source.MasterBus.SetInt(TimsNotchController.ResolvedPowerNotchKey, power);
            source.MasterBus.SetInt(TimsNotchController.ResolvedBrakeStepKey, brakeStep);
            source.MasterBus.SetBool(TimsNotchController.IsEmergencyBrakeRequestedKey, false);
            source.MasterBus.SetBool(TimsBrakeController.IsEmergencyKey, emergency);
            display.Refresh();
        }

        [Test]
        public void PrefabContainsCompleteOrderedScale()
        {
            Assert.That(displayObject.GetComponentsInChildren<PowerNotchCell>().Select(c => c.Notch),
                Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(displayObject.GetComponentsInChildren<BrakeNotchCell>().Select(c => c.Notch),
                Is.EqualTo(new[] { 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(displayObject.GetComponentsInChildren<NeutralNotchCell>().Length, Is.EqualTo(1));
            Assert.That(displayObject.GetComponentsInChildren<EmergencyNotchCell>().Length, Is.EqualTo(1));
            foreach (var text in displayObject.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.font, Is.Not.Null, text.name);
        }

        [Test]
        public void PowerFillsLowerCellsAndKeepsUnlitLabelsVisible()
        {
            Publish(3, 0);
            var cells = displayObject.GetComponentsInChildren<PowerNotchCell>();
            Assert.That(cells.Select(c => c.State), Is.EqualTo(new[]
                { PowerNotchCellState.Blank, PowerNotchCellState.Blank, PowerNotchCellState.On, PowerNotchCellState.Off }));
            foreach (var cell in cells)
                foreach (var text in cell.GetComponentsInChildren<TMP_Text>())
                    Assert.That(text.enabled, Is.EqualTo(cell.Notch >= 3));
            Assert.That(displayObject.GetComponentInChildren<NeutralNotchCell>().State, Is.EqualTo(NeutralNotchCellState.Off));
        }

        [TestCase(1, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(17, 5)]
        [TestCase(25, 7)]
        public void ContinuousBrakeStepUsesConfiguredSubsteps(int step, int expectedNotch)
        {
            Publish(4, step);
            foreach (var cell in displayObject.GetComponentsInChildren<BrakeNotchCell>())
                Assert.That(cell.State, Is.EqualTo(cell.Notch == expectedNotch ? BrakeNotchCellState.On :
                    cell.Notch < expectedNotch ? BrakeNotchCellState.Blank : BrakeNotchCellState.Off));
            Assert.That(displayObject.GetComponentsInChildren<PowerNotchCell>().All(c => c.State == PowerNotchCellState.Off), Is.True);
            settings.brakeSubstepCount = 2;
            Publish(0, 5);
            Assert.That(displayObject.GetComponentsInChildren<BrakeNotchCell>().Single(c => c.Notch == 3).State,
                Is.EqualTo(BrakeNotchCellState.On));
        }

        [Test]
        public void EmergencyLatchOverridesNeutralAndSurvivesMissingNormalTags()
        {
            var emergencyBackground = displayObject.GetComponentInChildren<EmergencyNotchCell>().GetComponentInChildren<Image>();
            var neutralBackground = displayObject.GetComponentInChildren<NeutralNotchCell>().GetComponentInChildren<Image>();
            var emergencyOff = emergencyBackground.color;
            var neutralOff = neutralBackground.color;
            Publish(0, 0, true);
            source.MasterBus.Remove(TimsNotchController.ResolvedPowerNotchKey);
            display.Refresh();
            Assert.That(display.IsAvailable, Is.True);
            Assert.That(displayObject.GetComponentInChildren<EmergencyNotchCell>().State, Is.EqualTo(EmergencyNotchCellState.On));
            Assert.That(emergencyBackground.color, Is.Not.EqualTo(emergencyOff));
            Assert.That(displayObject.GetComponentInChildren<NeutralNotchCell>().State, Is.EqualTo(NeutralNotchCellState.Off));
            Publish(0, 0);
            Assert.That(displayObject.GetComponentInChildren<EmergencyNotchCell>().State, Is.EqualTo(EmergencyNotchCellState.Off));
            Assert.That(displayObject.GetComponentInChildren<NeutralNotchCell>().State, Is.EqualTo(NeutralNotchCellState.On));
            Assert.That(neutralBackground.color, Is.Not.EqualTo(neutralOff));
        }

        [Test]
        public void MissingInputClearsPreviousDisplayAndDoesNotShowNeutral()
        {
            Publish(3, 0);
            source.MasterBus.Remove(TimsNotchController.ResolvedBrakeStepKey);
            display.Refresh();
            AssertUnavailable();
            Publish(0, 0);
            Assert.That(display.IsAvailable, Is.True);
            Assert.That(displayObject.transform.Find("Unavailable").GetComponent<TMP_Text>().enabled, Is.False);
            source.enabled = false;
            display.Refresh();
            AssertUnavailable();
        }

        [TestCase(-1, 0)]
        [TestCase(5, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 26)]
        public void InvalidValuesAreUnavailable(int power, int brake)
        {
            Publish(power, brake);
            AssertUnavailable();
        }

        [Test]
        public void ExplicitSourceCanBeDisconnectedWithoutKeepingOldValues()
        {
            Publish(4, 0);
            display.Configure(null);
            AssertUnavailable();
            display.Configure(source);
            Assert.That(display.IsAvailable, Is.True);
        }

        private void AssertUnavailable()
        {
            Assert.That(display.IsAvailable, Is.False);
            Assert.That(displayObject.GetComponentsInChildren<PowerNotchCell>().All(c => c.State == PowerNotchCellState.Off), Is.True);
            Assert.That(displayObject.GetComponentsInChildren<BrakeNotchCell>().All(c => c.State == BrakeNotchCellState.Off), Is.True);
            Assert.That(displayObject.GetComponentInChildren<NeutralNotchCell>().State, Is.EqualTo(NeutralNotchCellState.Off));
            Assert.That(displayObject.GetComponentInChildren<EmergencyNotchCell>().State, Is.EqualTo(EmergencyNotchCellState.Off));
            Assert.That(displayObject.transform.Find("Unavailable").GetComponent<TMP_Text>().enabled, Is.False);
        }
    }
}
