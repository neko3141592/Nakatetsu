using System.Collections.Generic;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Communication;
using System;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Presentation.Indicators;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.TrainStatus
{
    /// <summary>編成順の車体・号車番号と、各車LocalBusの実測牽引力を表示する。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Nakatetsu/Tims/Train Status Display")]
    public sealed class TimsTrainStatusDisplay : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("未指定なら親TIMS、または同じTrainRoot内のTIMSを使用します。編成定義はTrainRootから取得します。")]
        [SerializeField] private TimsCommunicationController tims;
        [Tooltip("編集用の見本。実行時は非表示にし、生成した車両表示に置き換えます。")]
        [SerializeField] private GameObject editorSample;

        [Serializable]
        public sealed class StateBinding
        {
            [Tooltip("Local: 表示中の各号車のLocalBus。Master: 全車共通のMasterBus。")]
            public TimsBusTarget busTarget = TimsBusTarget.Local;
            public string deviceName = "Traction";
            public string itemName = "ActualTractionForceN";

            internal TimsBusState ResolveBus(TimsCommunicationController source, int carIndex)
            {
                if (source == null || string.IsNullOrWhiteSpace(deviceName) ||
                    string.IsNullOrWhiteSpace(itemName)) return null;
                if (busTarget == TimsBusTarget.Master) return source.MasterBus;
                return source.TryGetLocalBus(carIndex, out var bus) ? bus : null;
            }

            internal TimsTagKey Key => new(deviceName, itemName);
        }

        [Header("State Binding")]
        [Tooltip("符号付き実測牽引力(float/N)の参照先。+1N超が力行、-1N未満が回生。それ以外は惰性。")]
        [SerializeField] private StateBinding tractionBinding = new();
        [Tooltip("有効性タグ(bool)がtrueのときだけ状態を読みます。独自タグに有効性情報がない場合はオフにできます。")]
        [SerializeField] private bool checkAvailability = true;
        [SerializeField] private StateBinding availabilityBinding = new() { itemName = "IsAvailable" };

        [Header("Direction Arrow")]
        [SerializeField] private Sprite directionArrowSprite;
        [Tooltip("透明余白を含むRectサイズ。付属の矢印は320×320で実際の図柄が約42×16になります。") ]
        [SerializeField] private Vector2 directionArrowSize = new(320f, 320f);
        [Tooltip("元画像が右向きの場合はオン。左向き画像の場合はオフ。")]
        [SerializeField] private bool directionArrowSpritePointsRight = true;
        [Tooltip("有効運転台のintタグ。先頭=1、後部=-1、未選択=0。")]
        [SerializeField] private StateBinding activatedCabBinding = new()
            { busTarget = TimsBusTarget.Master, deviceName = "Direction", itemName = "ActivatedCabPosition" };
        [Tooltip("有効運転台から見たレバーサのintタグ。前進=1、後進=-1、中立=0。Localの場合は有効運転台の号車を読む。")]
        [SerializeField] private StateBinding reverserBinding = new()
            { busTarget = TimsBusTarget.Master, deviceName = "Direction", itemName = "ReverserPosition" };

        [Header("Layout")]
        [Tooltip("編成全体の中心位置。両数が変わってもこの位置を中心に車両を並べます。矢印は中央揃えの計算に含めません。") ]
        [FormerlySerializedAs("firstCarPosition")]
        [SerializeField] private Vector2 centerPosition = new(0f, -51.1f);
        [SerializeField, Min(0f)] private float spacingX = 80f;
        [Tooltip("画像の透明余白も含むRectのサイズ。付属スプライトは1024×1024で見本と同じ大きさです。")]
        [SerializeField] private Vector2 spriteSize = new(1024f, 1024f);

        [Header("Sprites")]
        [SerializeField] private Sprite trailerSprite;
        [SerializeField] private Sprite trailerCabSprite;
        [SerializeField] private Sprite motorCoastingSprite;
        [SerializeField] private Sprite motorPowerSprite;
        [SerializeField] private Sprite motorRegenSprite;
        [SerializeField] private Sprite motorCabCoastingSprite;
        [SerializeField] private Sprite motorCabPowerSprite;
        [SerializeField] private Sprite motorCabRegenSprite;

        [Header("Pantograph")]
        [Tooltip("パンタグラフ搭載車に表示するMain Active画像。シミュレーション実装までは常時この画像を表示します。")]
        [SerializeField] private Sprite pantographMainActiveSprite;

        [Header("Car Numbers")]
        [SerializeField] private TMP_FontAsset numberFont;
        [SerializeField, Min(1f)] private float numberFontSize = 24f;
        [SerializeField] private Vector2 numberOffset;
        [SerializeField] private Vector2 numberSize = new(76f, 36f);
        [SerializeField] private Color coastingNumberColor = Color.white;
        [Tooltip("力行・回生の両方で使用する号車番号の色。")]
        [SerializeField] private Color activeNumberColor = Color.black;

        [Header("Traction / Regen")]
        [Tooltip("オフにすると全車を惰性のスプライト・番号色で表示します。車両の制御状態は変更しません。")]
        [SerializeField] private bool showTractionRegenState = true;

        [SerializeField, HideInInspector] private RectTransform generatedRoot;
        private readonly List<CarVisual> cars = new();
        private Image directionArrow;

        private sealed class CarVisual
        {
            public Image image;
            public Image pantograph;
            public TextMeshProUGUI number;
        }

        public int GeneratedCarCount => cars.Count;
        public RectTransform GeneratedRoot => generatedRoot;

        private void OnEnable()
        {
            if (Application.isPlaying && editorSample != null) editorSample.SetActive(false);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void OnDisable()
        {
            if (generatedRoot != null) generatedRoot.gameObject.SetActive(false);
        }

        private void OnDestroy() => ClearGenerated();

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            Refresh();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            TrainRoot owner = ResolveOwner();
            ConsistDefinitionAsset definition = owner != null ? owner.ConsistDefinition : null;
            int count = definition != null ? definition.CarCount : 0;
            if (count == 0)
            {
                ClearGenerated();
                return;
            }

            if (NeedsRebuild(count)) Build(count);
            if (editorSample != null) editorSample.SetActive(false);
            generatedRoot.gameObject.SetActive(true);
            bool sourceAvailable = tims != null && tims.isActiveAndEnabled && owner.isActiveAndEnabled;
            Vector2 firstCarPosition = centerPosition - Vector2.right * (spacingX * (count - 1) * .5f);
            for (int i = 0; i < count; i++)
            {
                CarVisual visual = cars[i];
                CarDefinitionAsset car = definition.cars[i];
                // 欠けた車両定義は別車種で埋めず、位置だけ確保する。
                visual.image.gameObject.SetActive(car != null);
                visual.number.gameObject.SetActive(car != null);
                visual.pantograph.gameObject.SetActive(car != null && car.hasPantograph && pantographMainActiveSprite != null);
                if (car == null) continue;

                // 旧版と同じく編成の両端に運転台を表示。Mc用スプライトも選択する。
                bool cab = i == 0 || i == count - 1;
                bool motor = car.motorCount > 0 || car.carType == CarType.M ||
                    car.carType == CarType.Mc1 || car.carType == CarType.Mc2;
                float forceN = 0f;
                bool measured = sourceAvailable && TryReadForce(i, out forceN);
                TrainStatusMotion motion = TrainStatusDisplayLogic.ResolveMotion(
                    showTractionRegenState, motor, measured, forceN);
                Sprite sprite = SelectSprite(cab, motor, motion);
                visual.image.sprite = sprite;
                visual.image.enabled = sprite != null;

                Vector2 position = firstCarPosition + Vector2.right * (spacingX * i);
                RectTransform rect = visual.image.rectTransform;
                rect.anchoredPosition = position;
                rect.sizeDelta = spriteSize;
                // 見本の左向き素材を末尾のみ反転。番号は別要素なので反転しない。
                rect.localScale = new Vector3(cab && i == count - 1 && i > 0 ? -1f : 1f, 1f, 1f);
                // 素材内の余白がパンタ位置を決めるため、車体と同じRectに重ねる。
                // 車体の先頭・末尾反転や力行状態には連動させない。
                visual.pantograph.sprite = pantographMainActiveSprite;
                visual.pantograph.rectTransform.anchoredPosition = position;
                visual.pantograph.rectTransform.sizeDelta = spriteSize;
                visual.number.rectTransform.anchoredPosition = position + numberOffset;
                visual.number.rectTransform.sizeDelta = numberSize;
                TMP_FontAsset font = numberFont != null ? numberFont : TMP_Settings.defaultFontAsset;
                if (visual.number.font != font) visual.number.font = font;
                visual.number.fontSize = numberFontSize;
                visual.number.color = motion == TrainStatusMotion.Power || motion == TrainStatusMotion.Regen
                    ? activeNumberColor : coastingNumberColor;
            }
            RefreshDirectionArrow(count, sourceAvailable, firstCarPosition);
        }

        private void RefreshDirectionArrow(int count, bool sourceAvailable, Vector2 firstCarPosition)
        {
            int direction = 0;
            if (sourceAvailable)
            {
                // 運転台選択は編成全体の値。Local指定時は先頭車のタグを読む。
                TimsBusState cabBus = activatedCabBinding?.ResolveBus(tims, 0);
                if (cabBus != null && cabBus.TryGetInt(activatedCabBinding.Key, out int cab))
                {
                    int activeCarIndex = cab == -1 ? count - 1 : 0;
                    TimsBusState reverserBus = reverserBinding?.ResolveBus(tims, activeCarIndex);
                    if (reverserBus != null && reverserBus.TryGetInt(reverserBinding.Key, out int reverser))
                        direction = TrainStatusDisplayLogic.ResolveDirection(cab, reverser);
                }
            }
            directionArrow.sprite = directionArrowSprite;
            directionArrow.gameObject.SetActive(direction != 0 && directionArrowSprite != null);
            // 矢印を号車として数えず、1号車の位置から外側へ1間隔置く。
            directionArrow.rectTransform.anchoredPosition = firstCarPosition +
                Vector2.right * (spacingX * (direction > 0 ? -1 : count));
            directionArrow.rectTransform.sizeDelta = directionArrowSize;
            bool pointsRight = direction < 0;
            directionArrow.rectTransform.localScale = new Vector3(
                pointsRight == directionArrowSpritePointsRight ? 1f : -1f, 1f, 1f);
        }

        private bool TryReadForce(int carIndex, out float forceN)
        {
            forceN = 0f;
            if (checkAvailability)
            {
                TimsBusState availabilityBus = availabilityBinding?.ResolveBus(tims, carIndex);
                if (availabilityBus == null ||
                    !availabilityBus.TryGetBool(availabilityBinding.Key, out bool available) || !available)
                    return false;
            }
            TimsBusState bus = tractionBinding?.ResolveBus(tims, carIndex);
            return bus != null && bus.TryGetFloat(tractionBinding.Key, out forceN);
        }

        private TrainRoot ResolveOwner()
        {
            if (tims == null)
            {
                tims = GetComponentInParent<TimsCommunicationController>(true);
                if (tims == null)
                {
                    var train = GetComponentInParent<TrainRoot>(true);
                    if (train != null) tims = train.GetComponentInChildren<TimsCommunicationController>(true);
                }
            }
            return tims != null ? tims.GetComponentInParent<TrainRoot>(true) : GetComponentInParent<TrainRoot>(true);
        }

        private Sprite SelectSprite(bool cab, bool motor, TrainStatusMotion motion)
        {
            if (!motor) return cab ? trailerCabSprite : trailerSprite;
            Sprite coast = cab ? motorCabCoastingSprite : motorCoastingSprite;
            Sprite active = motion == TrainStatusMotion.Power ? (cab ? motorCabPowerSprite : motorPowerSprite)
                : motion == TrainStatusMotion.Regen ? (cab ? motorCabRegenSprite : motorRegenSprite) : null;
            // 欠測時も古い力行・回生表示を残さず、塗りのない惰性用画像へ戻す。
            return active != null ? active : coast;
        }

        private bool NeedsRebuild(int count)
        {
            if (generatedRoot == null || directionArrow == null || cars.Count != count) return true;
            foreach (CarVisual car in cars)
                if (car.image == null || car.pantograph == null || car.number == null) return true;
            return false;
        }

        private void Build(int count)
        {
            ClearGenerated();
            generatedRoot = CreateRect("GeneratedCars", transform);
            generatedRoot.anchorMin = Vector2.zero;
            generatedRoot.anchorMax = Vector2.one;
            generatedRoot.sizeDelta = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                RectTransform imageRect = CreateRect($"Car_{i + 1:00}", generatedRoot);
                Image image = imageRect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                cars.Add(new CarVisual { image = image });
            }
            // 車体画像より手前に重ねる。未搭載車は非表示。
            for (int i = 0; i < count; i++)
            {
                var pantograph = CreateRect($"Pantograph_{i + 1:00}", generatedRoot).gameObject.AddComponent<Image>();
                pantograph.preserveAspect = true;
                pantograph.raycastTarget = false;
                pantograph.gameObject.SetActive(false);
                cars[i].pantograph = pantograph;
            }
            directionArrow = CreateRect("DirectionArrow", generatedRoot).gameObject.AddComponent<Image>();
            directionArrow.preserveAspect = true;
            directionArrow.raycastTarget = false;
            directionArrow.gameObject.SetActive(false);
            // 全ての番号を画像より手前に描く（余白の大きい画像でも隠れない）。
            for (int i = 0; i < count; i++)
            {
                RectTransform labelRect = CreateRect($"CarNumber_{i + 1:00}", generatedRoot);
                var number = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                number.text = TrainStatusDisplayLogic.FormatCarNumber(i + 1);
                number.alignment = TextAlignmentOptions.Center;
                number.fontStyle = FontStyles.Normal;
                number.enableAutoSizing = false;
                number.textWrappingMode = TextWrappingModes.NoWrap;
                number.overflowMode = TextOverflowModes.Overflow;
                number.raycastTarget = false;
                cars[i].number = number;
            }
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        private void ClearGenerated()
        {
            cars.Clear();
            directionArrow = null;
            if (generatedRoot == null) return;
            GameObject root = generatedRoot.gameObject;
            generatedRoot = null;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }

        private void OnValidate()
        {
            spacingX = Mathf.Max(0f, spacingX);
            spriteSize = new Vector2(Mathf.Max(1f, spriteSize.x), Mathf.Max(1f, spriteSize.y));
            directionArrowSize = new Vector2(Mathf.Max(1f, directionArrowSize.x), Mathf.Max(1f, directionArrowSize.y));
            numberFontSize = Mathf.Max(1f, numberFontSize);
            numberSize = new Vector2(Mathf.Max(1f, numberSize.x), Mathf.Max(1f, numberSize.y));
        }
    }
}
