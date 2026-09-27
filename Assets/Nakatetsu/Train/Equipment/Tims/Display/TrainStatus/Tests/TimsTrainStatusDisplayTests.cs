using System.Reflection;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using Nakatetsu.Train.Equipment.Tims.Presentation.TrainStatus;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Tests
{
    public sealed class TimsTrainStatusDisplayTests
    {
        private GameObject train;
        private ConsistDefinitionAsset consist;
        private CarDefinitionAsset motor;
        private CarDefinitionAsset trailer;
        private TimsCommunicationController tims;
        private TimsTrainStatusDisplay display;
        private Texture2D texture;
        private Sprite coast, power, regen, cab, trailerImage;
        private GameObject sample, background;
        private static readonly TimsTagKey Force = new("CustomDrive", "MeasuredForce");

        [SetUp]
        public void SetUp()
        {
            consist = ScriptableObject.CreateInstance<ConsistDefinitionAsset>();
            motor = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            motor.carType = CarType.M; motor.motorCount = 4;
            trailer = ScriptableObject.CreateInstance<CarDefinitionAsset>();
            trailer.carType = CarType.T; trailer.motorCount = 0;
            for (int i = 0; i < 10; i++) consist.cars.Add(i == 0 || i == 9 || i == 4 ? trailer : motor);
            train = new GameObject("TrainStatusTest");
            train.AddComponent<TrainRoot>().Configure(consist);
            tims = train.AddComponent<TimsCommunicationController>();
            typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tims, null);
            var go = new GameObject("TrainStatus", typeof(RectTransform)); go.transform.SetParent(train.transform);
            display = go.AddComponent<TimsTrainStatusDisplay>();
            sample = new GameObject("Sample"); sample.transform.SetParent(go.transform);
            background = new GameObject("BackGround"); background.transform.SetParent(go.transform);
            texture = new Texture2D(4, 4);
            coast = NewSprite(); power = NewSprite(); regen = NewSprite(); cab = NewSprite(); trailerImage = NewSprite();
            Set("motorCoastingSprite", coast); Set("motorPowerSprite", power); Set("motorRegenSprite", regen);
            Set("motorCabCoastingSprite", coast); Set("motorCabPowerSprite", power); Set("motorCabRegenSprite", regen);
            Set("trailerCabSprite", cab); Set("trailerSprite", trailerImage); Set("editorSample", sample);
            Set("tractionBinding", new TimsTrainStatusDisplay.StateBinding { deviceName = "CustomDrive", itemName = "MeasuredForce" });
            Set("checkAvailability", false);
            display.SetTimsSource(tims);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(train); Object.DestroyImmediate(consist);
            Object.DestroyImmediate(motor); Object.DestroyImmediate(trailer);
            foreach (var sprite in new[] { coast, power, regen, cab, trailerImage }) Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [TestCase(2f, TrainStatusMotion.Power)]
        [TestCase(-2f, TrainStatusMotion.Regen)]
        [TestCase(1f, TrainStatusMotion.Coasting)]
        [TestCase(-1f, TrainStatusMotion.Coasting)]
        [TestCase(0f, TrainStatusMotion.Coasting)]
        [TestCase(float.NaN, TrainStatusMotion.Unavailable)]
        [TestCase(float.PositiveInfinity, TrainStatusMotion.Unavailable)]
        public void RetainsLegacyMeasuredForceThreshold(float force, TrainStatusMotion expected) =>
            Assert.That(TrainStatusDisplayLogic.ResolveMotion(true, true, true, force), Is.EqualTo(expected));

        [Test]
        public void CustomLocalBindingShowsEachCarsStateAndTrailerAlwaysCoasts()
        {
            tims.GetLocalBus(1).SetFloat(Force, 500f);
            tims.GetLocalBus(2).SetFloat(Force, -500f);
            tims.GetLocalBus(3).SetFloat(Force, 0f);
            tims.GetLocalBus(4).SetFloat(Force, 500f);
            display.Refresh();
            Assert.That(Car(0).sprite, Is.SameAs(cab));
            Assert.That(Car(1).sprite, Is.SameAs(power)); Assert.That(Car(2).sprite, Is.SameAs(regen));
            Assert.That(Car(3).sprite, Is.SameAs(coast)); Assert.That(Car(4).sprite, Is.SameAs(trailerImage));
            Assert.That(Number(1).color, Is.EqualTo(Color.black)); Assert.That(Number(2).color, Is.EqualTo(Color.black));
            Assert.That(Number(3).color, Is.EqualTo(Color.white));
            tims.GetLocalBus(1).SetFloat(Force, -200f); display.Refresh();
            Assert.That(Car(1).sprite, Is.SameAs(regen));
        }

        [Test]
        public void ForceCoastingOverridesPowerAndRegenWithoutWritingBus()
        {
            tims.GetLocalBus(1).SetFloat(Force, 500f); tims.GetLocalBus(2).SetFloat(Force, -500f);
            Set("showTractionRegenState", false); display.Refresh();
            Assert.That(Car(1).sprite, Is.SameAs(coast)); Assert.That(Car(2).sprite, Is.SameAs(coast));
            Assert.That(Number(1).color, Is.EqualTo(Color.white)); Assert.That(Number(2).color, Is.EqualTo(Color.white));
            Assert.That(tims.GetLocalBus(1).TryGetFloat(Force, out float actual), Is.True);
            Assert.That(actual, Is.EqualTo(500f));
        }

        [Test]
        public void LayoutUsesSharedSpacingFullWidthNumbersAndUnmirroredLabels()
        {
            Set("spacingX", 95f); Set("numberOffset", new Vector2(2f, 3f)); display.Refresh();
            Assert.That(Number(9).text, Is.EqualTo("１０")); Assert.That(Number(0).text, Is.EqualTo("１"));
            Assert.That(Car(1).rectTransform.anchoredPosition.x - Car(0).rectTransform.anchoredPosition.x, Is.EqualTo(95f));
            Assert.That(Number(1).rectTransform.anchoredPosition.x - Number(0).rectTransform.anchoredPosition.x, Is.EqualTo(95f));
            Assert.That(Number(0).rectTransform.anchoredPosition - Car(0).rectTransform.anchoredPosition, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(Car(0).transform.localScale.x, Is.EqualTo(1f));
            Assert.That(Car(9).transform.localScale.x, Is.EqualTo(-1f)); Assert.That(Number(0).transform.localScale.x, Is.EqualTo(1f));
            Assert.That(sample.activeSelf, Is.False); Assert.That(background.activeSelf, Is.True);
        }

        [Test]
        public void MissingWrongTypeUnavailableAndDisabledSourceClearPreviousPower()
        {
            tims.GetLocalBus(1).SetFloat(Force, 500f); display.Refresh(); Assert.That(Car(1).sprite, Is.SameAs(power));
            tims.GetLocalBus(1).SetBool(Force, true); display.Refresh(); Assert.That(Car(1).sprite, Is.SameAs(coast));
            tims.GetLocalBus(1).SetFloat(Force, 500f);
            Set("checkAvailability", true);
            Set("availabilityBinding", new TimsTrainStatusDisplay.StateBinding { deviceName = "CustomDrive", itemName = "Online" });
            display.Refresh(); Assert.That(Car(1).sprite, Is.SameAs(coast));
            tims.GetLocalBus(1).SetBool(new TimsTagKey("CustomDrive", "Online"), true);
            display.Refresh(); Assert.That(Car(1).sprite, Is.SameAs(power));
            tims.enabled = false; display.Refresh(); Assert.That(Car(1).sprite, Is.SameAs(coast));
        }

        [Test]
        public void MasterBindingAndMotorCabSpritesAreSupported()
        {
            consist.cars[0] = motor;
            Set("tractionBinding", new TimsTrainStatusDisplay.StateBinding { busTarget = TimsBusTarget.Master, deviceName = "CustomDrive", itemName = "MeasuredForce" });
            tims.MasterBus.SetFloat(Force, -500f); display.Refresh();
            Assert.That(Car(0).sprite, Is.SameAs(regen)); Assert.That(Car(1).sprite, Is.SameAs(regen));
            Assert.That(Car(4).sprite, Is.SameAs(trailerImage));
        }

        [Test]
        public void RebuildOnlyReplacesGeneratedObjectsAndRebindingDoesNotLeakOtherTrainState()
        {
            tims.GetLocalBus(1).SetFloat(Force, 500f); display.Refresh();
            var other = new GameObject("OtherTrain");
            try
            {
                other.AddComponent<TrainRoot>().Configure(consist);
                var otherTims = other.AddComponent<TimsCommunicationController>();
                typeof(TimsCommunicationController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(otherTims, null);
                display.SetTimsSource(otherTims); Assert.That(Car(1).sprite, Is.SameAs(coast));
                display.SetTimsSource(tims); Assert.That(Car(1).sprite, Is.SameAs(power));
                consist.cars.RemoveRange(3, 7); display.Refresh();
                Assert.That(display.GeneratedCarCount, Is.EqualTo(3));
                Assert.That(display.GeneratedRoot.childCount, Is.EqualTo(10));
                Assert.That(sample != null && background != null, Is.True);
                consist.cars.Clear(); display.Refresh(); Assert.That(display.GeneratedRoot == null, Is.True);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [TestCase(1, 1, -440f, -1f)]
        [TestCase(1, -1, 440f, 1f)]
        [TestCase(-1, 1, 440f, 1f)]
        [TestCase(-1, -1, -440f, -1f)]
        public void ArrowUsesCabAndReverserWithoutShiftingCars(int cabPosition, int reverser, float x, float scale)
        {
            Set("directionArrowSprite", power);
            Set("centerPosition", Vector2.zero); Set("spacingX", 80f);
            Set("activatedCabBinding", new TimsTrainStatusDisplay.StateBinding
                { busTarget = TimsBusTarget.Master, deviceName = "TestDirection", itemName = "Cab" });
            Set("reverserBinding", new TimsTrainStatusDisplay.StateBinding
                { busTarget = TimsBusTarget.Master, deviceName = "TestDirection", itemName = "Reverser" });
            tims.MasterBus.SetInt(new TimsTagKey("TestDirection", "Cab"), cabPosition);
            tims.MasterBus.SetInt(new TimsTagKey("TestDirection", "Reverser"), reverser);
            Set("showTractionRegenState", false);
            display.Refresh();
            var arrow = display.GeneratedRoot.Find("DirectionArrow").GetComponent<Image>();
            Assert.That(arrow.gameObject.activeSelf, Is.True);
            Assert.That(arrow.rectTransform.anchoredPosition.x, Is.EqualTo(x));
            Assert.That(arrow.transform.localScale.x, Is.EqualTo(scale));
            Assert.That(Car(0).rectTransform.anchoredPosition.x, Is.EqualTo(-360f));
            Assert.That(Car(9).rectTransform.anchoredPosition.x, Is.EqualTo(360f));
            Assert.That(Number(0).rectTransform.anchoredPosition.x, Is.EqualTo(-360f));
            Assert.That(Number(9).text, Is.EqualTo("１０"));
            Assert.That(display.GeneratedCarCount, Is.EqualTo(10));
            Set("directionArrowSpritePointsRight", false); display.Refresh();
            Assert.That(arrow.transform.localScale.x, Is.EqualTo(-scale));
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(99, 1)]
        [TestCase(1, -99)]
        public void NeutralOrInvalidDirectionHidesPreviouslyVisibleArrow(int cabPosition, int reverser)
        {
            Set("directionArrowSprite", power);
            var cabKey = new TimsTagKey("Direction", "ActivatedCabPosition");
            var reverserKey = new TimsTagKey("Direction", "ReverserPosition");
            tims.MasterBus.SetInt(cabKey, 1); tims.MasterBus.SetInt(reverserKey, 1); display.Refresh();
            var arrow = display.GeneratedRoot.Find("DirectionArrow");
            Assert.That(arrow.gameObject.activeSelf, Is.True);
            tims.MasterBus.SetInt(cabKey, cabPosition); tims.MasterBus.SetInt(reverserKey, reverser); display.Refresh();
            Assert.That(arrow.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void LocalReverserReadsActiveCabAndArrowFollowsActualFormationLength()
        {
            Set("directionArrowSprite", power); Set("centerPosition", Vector2.zero);
            Set("reverserBinding", new TimsTrainStatusDisplay.StateBinding
                { deviceName = "MasterController", itemName = "ReverserPosition" });
            var key = new TimsTagKey("MasterController", "ReverserPosition");
            tims.MasterBus.SetInt(new TimsTagKey("Direction", "ActivatedCabPosition"), -1);
            tims.GetLocalBus(0).SetInt(key, -1); tims.GetLocalBus(9).SetInt(key, 1); display.Refresh();
            Assert.That(display.GeneratedRoot.Find("DirectionArrow").gameObject.activeSelf, Is.True);
            Assert.That(((RectTransform)display.GeneratedRoot.Find("DirectionArrow")).anchoredPosition.x, Is.EqualTo(440f));
            consist.cars.RemoveRange(3, 7); tims.GetLocalBus(2).SetInt(key, 1); display.Refresh();
            var arrow = (RectTransform)display.GeneratedRoot.Find("DirectionArrow");
            Assert.That(arrow.anchoredPosition.x, Is.EqualTo(160f));
            tims.GetLocalBus(2).Remove(key); display.Refresh(); Assert.That(arrow.gameObject.activeSelf, Is.False);
            tims.GetLocalBus(2).SetBool(key, true); display.Refresh(); Assert.That(arrow.gameObject.activeSelf, Is.False);
            tims.GetLocalBus(2).SetInt(key, 1); display.Refresh(); Assert.That(arrow.gameObject.activeSelf, Is.True);
            tims.enabled = false; display.Refresh(); Assert.That(arrow.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ChangingCarCountKeepsFormationCenteredAndArrowOutside()
        {
            var center = new Vector2(125f, -35f);
            Set("centerPosition", center); Set("spacingX", 80f); Set("directionArrowSprite", power);
            tims.MasterBus.SetInt(new TimsTagKey("Direction", "ActivatedCabPosition"), 1);
            tims.MasterBus.SetInt(new TimsTagKey("Direction", "ReverserPosition"), 1);
            foreach (int count in new[] { 10, 4, 3, 1, 6 })
            {
                consist.cars.Clear();
                for (int i = 0; i < count; i++) consist.cars.Add(motor);
                display.Refresh();
                Vector2 first = Car(0).rectTransform.anchoredPosition;
                Vector2 last = Car(count - 1).rectTransform.anchoredPosition;
                Assert.That((first + last) * .5f, Is.EqualTo(center), $"{count} cars");
                Assert.That(last.x - first.x, Is.EqualTo(80f * (count - 1)));
                Assert.That(Number(0).rectTransform.anchoredPosition, Is.EqualTo(first));
                Assert.That(Number(count - 1).rectTransform.anchoredPosition, Is.EqualTo(last));
                var arrow = (RectTransform)display.GeneratedRoot.Find("DirectionArrow");
                Assert.That(arrow.anchoredPosition, Is.EqualTo(first - Vector2.right * 80f));
                tims.MasterBus.SetInt(new TimsTagKey("Direction", "ReverserPosition"), -1); display.Refresh();
                Assert.That(arrow.anchoredPosition, Is.EqualTo(last + Vector2.right * 80f));
                Assert.That((Car(0).rectTransform.anchoredPosition + Car(count - 1).rectTransform.anchoredPosition) * .5f, Is.EqualTo(center));
                tims.MasterBus.SetInt(new TimsTagKey("Direction", "ReverserPosition"), 1);
            }
        }

        [Test]
        public void PantographsOnlyOverlayEquippedCarsAtTheSameRect()
        {
            motor.hasPantograph = true;
            Set("pantographMainActiveSprite", power);
            Set("centerPosition", new Vector2(32f, -50f)); Set("spacingX", 95f);
            Set("spriteSize", new Vector2(800f, 800f)); display.Refresh();
            for (int i = 0; i < 10; i++)
            {
                var panto = display.GeneratedRoot.Find($"Pantograph_{i + 1:00}").GetComponent<Image>();
                Assert.That(panto.gameObject.activeSelf, Is.EqualTo(consist.cars[i].hasPantograph));
                Assert.That(panto.sprite, Is.SameAs(power));
                Assert.That(panto.rectTransform.anchoredPosition, Is.EqualTo(Car(i).rectTransform.anchoredPosition));
                Assert.That(panto.rectTransform.sizeDelta, Is.EqualTo(Car(i).rectTransform.sizeDelta));
                Assert.That(panto.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(panto.transform.GetSiblingIndex(), Is.GreaterThan(Car(i).transform.GetSiblingIndex()));
            }
            // 搭載の有無は電動車・付随車やTIMS状態に依存しない。
            motor.hasPantograph = false; trailer.hasPantograph = true;
            Set("showTractionRegenState", false); tims.enabled = false; display.Refresh();
            Assert.That(display.GeneratedRoot.Find("Pantograph_01").gameObject.activeSelf, Is.True);
            Assert.That(display.GeneratedRoot.Find("Pantograph_02").gameObject.activeSelf, Is.False);
            consist.cars[0] = null; display.Refresh();
            Assert.That(display.GeneratedRoot.Find("Pantograph_01").gameObject.activeSelf, Is.False);
            Set("pantographMainActiveSprite", null); display.Refresh();
            Assert.That(display.GeneratedRoot.Find("Pantograph_10").gameObject.activeSelf, Is.False);
        }

        private Sprite NewSprite() => Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        private Image Car(int index) => display.GeneratedRoot.Find($"Car_{index + 1:00}").GetComponent<Image>();
        private TMP_Text Number(int index) => display.GeneratedRoot.Find($"CarNumber_{index + 1:00}").GetComponent<TMP_Text>();
        private void Set(string name, object value) => typeof(TimsTrainStatusDisplay).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(display, value);
    }
}
