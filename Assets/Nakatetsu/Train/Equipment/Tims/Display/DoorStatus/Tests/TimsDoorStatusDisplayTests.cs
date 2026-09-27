using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.DoorStatus;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsDoorStatusDisplayTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset car;
        private TimsCommunicationController tims;
        private TimsDoorStatusDisplay display;
        private Texture2D texture;
        private Sprite open, closed;
        private readonly TimsTagKey key = new("Door", "AllClosed");

        [SetUp]
        public void SetUp()
        {
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            car = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            for (int i = 0; i < 10; i++) consist.cars.Add(car);
            train = new GameObject("DoorStatusTest"); train.AddComponent<TrainRoot>().Configure(consist);
            tims = train.AddComponent<TimsCommunicationController>();
            typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tims, null);
            var go = new GameObject("DoorStatus", typeof(RectTransform)); go.transform.SetParent(train.transform);
            display = go.AddComponent<TimsDoorStatusDisplay>();
            texture = new Texture2D(4, 4);
            open = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            closed = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            Set("openSprite", open); Set("closedSprite", closed);
            Set("labelFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Nakatetsu/Content/Fonts/Noto/NotoSansJP-Regular SDF.asset"));
            display.SetTimsSource(tims);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train); Object.DestroyImmediate(consist); Object.DestroyImmediate(car);
            Object.DestroyImmediate(open); Object.DestroyImmediate(closed); Object.DestroyImmediate(texture);
        }

        [Test]
        public void EachCarsContactSwitchesSpritesAndKeepsKaiLabel()
        {
            tims.GetLocalBus(0).SetBool(key, true); tims.GetLocalBus(1).SetBool(key, false); display.Refresh();
            Assert.That(Image(0).sprite, Is.SameAs(closed)); Assert.That(Image(1).sprite, Is.SameAs(open));
            for (int i = 0; i < 10; i++) Assert.That(Label(i).text, Is.EqualTo("開"));
            Assert.That(Label(0).color, Is.Not.EqualTo(Label(1).color));
            tims.GetLocalBus(0).SetBool(key, false); tims.GetLocalBus(1).SetBool(key, true); display.Refresh();
            Assert.That(Image(0).sprite, Is.SameAs(open)); Assert.That(Image(1).sprite, Is.SameAs(closed));
        }

        [Test]
        public void UnknownStateClearsOldSpriteWithoutClaimingClosed()
        {
            tims.GetLocalBus(0).SetBool(key, true); display.Refresh(); Assert.That(Image(0).enabled, Is.True);
            tims.GetLocalBus(0).Remove(key); display.Refresh(); Assert.That(Image(0).enabled, Is.False);
            Assert.That(Label(0).text, Is.EqualTo("開")); Assert.That(Label(0).color, Is.EqualTo(Color.gray));
            tims.GetLocalBus(0).SetInt(key, 1); display.Refresh(); Assert.That(Image(0).enabled, Is.False);
            tims.GetLocalBus(0).SetBool(key, false); display.Refresh(); Assert.That(Image(0).sprite, Is.SameAs(open));
            tims.enabled = false; display.Refresh(); Assert.That(Image(0).enabled, Is.False);
        }

        [Test]
        public void MasterOrLocalCustomBindingCanInvertBoolMeaning()
        {
            Set("busTarget", TimsBusTarget.Master); Set("deviceName", "CustomDoor"); Set("itemName", "IsOpen"); Set("valueMeansClosed", false);
            var custom = new TimsTagKey("CustomDoor", "IsOpen");
            tims.MasterBus.SetBool(custom, true); display.Refresh();
            Assert.That(Image(0).sprite, Is.SameAs(open)); Assert.That(Image(9).sprite, Is.SameAs(open));
            Set("busTarget", TimsBusTarget.Local); tims.GetLocalBus(0).SetBool(custom, false); display.Refresh();
            Assert.That(Image(0).sprite, Is.SameAs(closed)); Assert.That(Image(9).enabled, Is.False);
        }

        [Test]
        public void LayoutStaysCenteredAcrossCountsWithSharedLabelSpacing()
        {
            var center = new Vector2(25f, -80f); var offset = new Vector2(2f, 3f);
            Set("centerPosition", center); Set("spacingX", 90f); Set("labelOffset", offset); Set("labelFontSize", 30f);
            foreach (int count in new[] { 10, 4, 3, 1, 6 })
            {
                consist.cars.Clear(); for (int i = 0; i < count; i++) consist.cars.Add(car); display.Refresh();
                Vector2 first = Image(0).rectTransform.anchoredPosition, last = Image(count - 1).rectTransform.anchoredPosition;
                Assert.That((first + last) * .5f, Is.EqualTo(center));
                Assert.That(last.x - first.x, Is.EqualTo(90f * (count - 1)));
                Assert.That(Label(count - 1).rectTransform.anchoredPosition, Is.EqualTo(last + offset));
                Assert.That(Label(0).fontSize, Is.EqualTo(30f));
                Assert.That(Label(0).font.name, Is.EqualTo("NotoSansJP-Regular SDF"));
                Assert.That(display.GeneratedRoot.childCount, Is.EqualTo(count * 2));
            }
        }

        [Test]
        public void RebindingDoesNotReadOtherTrainAndPreservesNonGeneratedObjects()
        {
            var sample = new GameObject("Sample"); sample.transform.SetParent(display.transform); Set("editorSample", sample);
            var background = new GameObject("Background"); background.transform.SetParent(display.transform);
            tims.GetLocalBus(0).SetBool(key, false); display.Refresh();
            var other = new GameObject("OtherTrain");
            try
            {
                other.AddComponent<TrainRoot>().Configure(consist);
                var source = other.AddComponent<TimsCommunicationController>();
                typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(source, null);
                display.SetTimsSource(source); Assert.That(Image(0).enabled, Is.False);
                display.SetTimsSource(tims); Assert.That(Image(0).sprite, Is.SameAs(open));
                Assert.That(sample.activeSelf, Is.False); Assert.That(background.activeSelf, Is.True);
                consist.cars[0] = null; display.Refresh(); Assert.That(Label(0).gameObject.activeSelf, Is.False);
                consist.cars.Clear(); display.Refresh(); Assert.That(display.GeneratedRoot == null, Is.True);
                Assert.That(sample != null && background != null, Is.True);
            }
            finally { Object.DestroyImmediate(other); }
        }

        private Image Image(int i) => display.GeneratedRoot.Find($"Door_{i + 1:00}").GetComponent<Image>();
        private TMP_Text Label(int i) => display.GeneratedRoot.Find($"DoorLabel_{i + 1:00}").GetComponent<TMP_Text>();
        private void Set(string field, object value) => typeof(TimsDoorStatusDisplay).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(display, value);
    }
}
