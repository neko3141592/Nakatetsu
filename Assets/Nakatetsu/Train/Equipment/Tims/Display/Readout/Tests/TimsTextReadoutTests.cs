using System.Globalization;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.Readout;
using Nakatetsu.Train.Equipment.Tims.Speed;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsTextReadoutTests
    {
        [TestCase(50493d, "１４時０１分３３秒")]
        [TestCase(0d, "００時００分００秒")]
        [TestCase(86399.9d, "２３時５９分５９秒")]
        [TestCase(86400d, "００時００分００秒")]
        [TestCase(90061d, "０１時０１分０１秒")]
        [TestCase(-1d, "－－時－－分－－秒")]
        [TestCase(double.NaN, "－－時－－分－－秒")]
        [TestCase(double.PositiveInfinity, "－－時－－分－－秒")]
        public void TimeUsesFullWidthAndWrapsOnlyDisplay(double seconds, string expected)
        {
            Assert.That(TimsTextReadout.FormatTime(seconds), Is.EqualTo(expected));
        }

        [TestCase(77f, "７７")]
        [TestCase(0f, "０")]
        [TestCase(76.5f, "７７")]
        [TestCase(76.4f, "７６")]
        [TestCase(-1f, "－－")]
        [TestCase(float.NaN, "－－")]
        [TestCase(float.PositiveInfinity, "－－")]
        public void SpeedUsesFullWidthIntegerWithoutUnit(float speed, string expected)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(TimsTextReadout.FormatSpeed(speed), Is.EqualTo(expected));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void RefreshFindsOwnTimsAndPreservesPresentation()
        {
            var root = new GameObject("Readout test");
            root.SetActive(false);
            var consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            try
            {
                root.AddComponent<TrainRoot>().Configure(consist);
                var source = root.AddComponent<TimsCommunicationController>();
                var readout = root.AddComponent<TimsTextReadout>();
                var textObject = new GameObject("Speed", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                var text = textObject.AddComponent<TextMeshProUGUI>();
                text.fontSize = 37f;
                text.color = Color.cyan;
                text.rectTransform.anchoredPosition = new Vector2(42f, -28f);
                var serialized = new SerializedObject(readout);
                serialized.FindProperty("speedText").objectReferenceValue = text;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                source.MasterBus.SetFloat(TimsSpeedController.SpeedKmhKey, 77f);
                source.MasterBus.SetBool(TimsSpeedController.HasValidSpeedKey, true);
                readout.Refresh();
                Assert.That(text.text, Is.EqualTo("７７"));
                Assert.That(text.fontSize, Is.EqualTo(37f));
                Assert.That(text.color, Is.EqualTo(Color.cyan));
                Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(42f, -28f)));

                source.MasterBus.SetBool(TimsSpeedController.HasValidSpeedKey, false);
                readout.Refresh();
                Assert.That(text.text, Is.EqualTo("－－"));

                source.MasterBus.SetBool(TimsSpeedController.HasValidSpeedKey, true);
                serialized.FindProperty("tims").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                readout.Refresh();
                Assert.That(text.text, Is.EqualTo("７７"), "所属TIMSを再取得する");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(consist);
            }
        }

        [Test]
        public void FindsSiblingTimsWithinOwnTrainAndDoesNotUseOtherTrain()
        {
            var owner = new GameObject("Owner train");
            var other = new GameObject("Other train");
            owner.SetActive(false);
            other.SetActive(false);
            var consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            try
            {
                owner.AddComponent<TrainRoot>().Configure(consist);
                other.AddComponent<TrainRoot>().Configure(consist);
                var otherTims = other.AddComponent<TimsCommunicationController>();
                var timsObject = new GameObject("TIMS");
                timsObject.transform.SetParent(owner.transform, false);
                var ownTims = timsObject.AddComponent<TimsCommunicationController>();
                var panel = new GameObject("Panel", typeof(RectTransform));
                panel.transform.SetParent(owner.transform, false);
                var label = panel.AddComponent<TextMeshProUGUI>();
                var readout = panel.AddComponent<TimsTextReadout>();
                var serialized = new SerializedObject(readout);
                serialized.FindProperty("speedText").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                owner.SetActive(true);
                other.SetActive(true);
                ownTims.MasterBus.SetFloat(TimsSpeedController.SpeedKmhKey, 42f);
                otherTims.MasterBus.SetFloat(TimsSpeedController.SpeedKmhKey, 99f);
                readout.Refresh();
                Assert.That(label.text, Is.EqualTo("４２"));

                Object.DestroyImmediate(timsObject);
                readout.Refresh();
                Assert.That(label.text, Is.EqualTo("－－"), "別編成のTIMSは拾わない");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(consist);
            }
        }
    }
}
