using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Notch;
using TMPro;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Indicators
{
    /// <summary>編成の確定ノッチを読む表示専用コンポーネント。車両の指令は変更しない。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/Notch Display")]
    public sealed class TimsNotchDisplay : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private TMP_Text unavailableText;

        [Header("Cell Generation")]
        [Tooltip("接続先TIMSの設定を優先。未接続の制作シーンではこの設定を使用します。")]
        [SerializeField] private TimsSettingsAsset cellSettings;
        [SerializeField] private PowerNotchCell powerCellPrefab;
        [SerializeField] private BrakeNotchCell brakeCellPrefab;
        [SerializeField] private NeutralNotchCell neutralCellPrefab;
        [SerializeField] private EmergencyNotchCell emergencyCellPrefab;
        [Tooltip("50×50のセル間の隙間（Canvas単位）。上から非常、B最大～B1、N、P1～P最大。")]
        [SerializeField, Min(0f)] private float cellSpacing = 4f;

        private Transform generatedCells;
        private PowerNotchCell[] powerCells;
        private BrakeNotchCell[] brakeCells;
        private NeutralNotchCell neutralCell;
        private EmergencyNotchCell emergencyCell;

        public bool IsAvailable { get; private set; }

        private void Awake()
        {
            GenerateCells();
        }

        private void GenerateCells()
        {
            var root = tims != null ? tims.GetComponent<TimsRoot>() : null;
            var settings = root != null && root.Settings != null ? root.Settings : cellSettings;
            if (settings == null || settings.powerNotchCount < 1 || settings.brakeNotchCount < 1 ||
                powerCellPrefab == null || brakeCellPrefab == null || neutralCellPrefab == null || emergencyCellPrefab == null)
            {
                Debug.LogWarning("ノッチセル生成にはTIMS設定と4種類のセルPrefabが必要です。", this);
                return;
            }

            var container = new GameObject("Generated Notch Cells", typeof(RectTransform));
            container.layer = gameObject.layer;
            var rect = (RectTransform)container.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            int count = settings.powerNotchCount + settings.brakeNotchCount + 2;
            float height = count * 50f + (count - 1) * Mathf.Max(0f, cellSpacing);
            rect.sizeDelta = new Vector2(50f, height);
            generatedCells = rect;

            int index = 0;
            CreateCell(emergencyCellPrefab, "Emergency", index++).SetState(EmergencyNotchCellState.Off);
            for (int notch = settings.brakeNotchCount; notch >= 1; notch--)
                CreateCell(brakeCellPrefab, $"B{notch}", index++).Set(BrakeNotchCellState.Off, notch);
            CreateCell(neutralCellPrefab, "Neutral", index++).SetState(NeutralNotchCellState.Off);
            for (int notch = 1; notch <= settings.powerNotchCount; notch++)
                CreateCell(powerCellPrefab, $"P{notch}", index++).Set(PowerNotchCellState.Off, notch);

            if (unavailableText != null)
            {
                var label = unavailableText.rectTransform;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
                label.anchoredPosition = new Vector2(0f, -height - 18f);
            }
            if (transform is RectTransform displayRect)
                displayRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + (unavailableText != null ? 34f : 0f));
            CacheCells();
        }

        private T CreateCell<T>(T prefab, string cellName, int index) where T : Component
        {
            // セルPrefabのルートはTransformなので、配置用RectTransformで包む。
            var holder = new GameObject(cellName, typeof(RectTransform));
            holder.layer = gameObject.layer;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(generatedCells, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(50f, 50f);
            rect.anchoredPosition = new Vector2(0f, -25f - index * (50f + Mathf.Max(0f, cellSpacing)));
            var cell = Instantiate(prefab, rect);
            cell.transform.localPosition = Vector3.zero;
            cell.transform.localRotation = Quaternion.identity;
            cell.transform.localScale = Vector3.one;
            cell.gameObject.SetActive(true);
            return cell;
        }

        private void OnEnable()
        {
            CacheCells();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void OnDisable() => Apply(false, false, 0, 0);

        public void Configure(TimsCommunicationController source)
        {
            // 画面ごとに編成を明示する。シーン全体から別編成を自動検索しない。
            tims = source;
            CacheCells();
            Refresh();
        }

        public void Refresh()
        {
            if (powerCells == null) CacheCells();
            if (tims == null || !tims.isActiveAndEnabled)
            {
                Apply(false, false, 0, 0);
                return;
            }

            var bus = tims.MasterBus;
            bool hasBrakeEmergency = bus.TryGetBool(TimsBrakeController.IsEmergencyKey, out bool brakeEmergency);
            bool hasNotchEmergency = bus.TryGetBool(TimsNotchController.IsEmergencyBrakeRequestedKey, out bool notchEmergency);
            // EB等による非常保持も表示する。非常が既知なら通常ノッチの欠損より優先。
            if ((hasBrakeEmergency && brakeEmergency) || (hasNotchEmergency && notchEmergency))
            {
                Apply(true, true, 0, 0);
                return;
            }

            var root = tims.GetComponent<TimsRoot>();
            var settings = root != null ? root.Settings : null;
            if (!hasBrakeEmergency || !hasNotchEmergency || settings == null ||
                settings.brakeSubstepCount <= 0 || settings.powerNotchCount <= 0 || settings.brakeNotchCount <= 0 ||
                !bus.TryGetInt(TimsNotchController.ResolvedPowerNotchKey, out int power) ||
                !bus.TryGetInt(TimsNotchController.ResolvedBrakeStepKey, out int brakeStep) ||
                power < 0 || power > settings.powerNotchCount || brakeStep < 0 ||
                brakeStep > (long)(settings.brakeNotchCount - 1) * settings.brakeSubstepCount + 1)
            {
                Apply(false, false, 0, 0);
                return;
            }

            int brake = 0;
            if (brakeStep > 0)
                TimsNotchCalculator.ToBrakeNotchStep(brakeStep, settings.brakeSubstepCount, out brake, out _);

            // 連続Stepは対応するB段へ表示変換するだけ。減速度・指令の再計算はしない。
            bool hasCell = brake > 0 ? HasBrakeCell(brake) : power == 0 || HasPowerCell(power);
            Apply(hasCell, false, brake > 0 ? 0 : power, brake);
        }

        private void CacheCells()
        {
            Transform cellsRoot = generatedCells != null ? generatedCells : transform;
            powerCells = cellsRoot.GetComponentsInChildren<PowerNotchCell>(true);
            brakeCells = cellsRoot.GetComponentsInChildren<BrakeNotchCell>(true);
            neutralCell = cellsRoot.GetComponentInChildren<NeutralNotchCell>(true);
            emergencyCell = cellsRoot.GetComponentInChildren<EmergencyNotchCell>(true);
        }

        private bool HasPowerCell(int notch)
        {
            foreach (var cell in powerCells) if (cell != null && cell.Notch == notch) return true;
            return false;
        }

        private bool HasBrakeCell(int notch)
        {
            foreach (var cell in brakeCells) if (cell != null && cell.Notch == notch) return true;
            return false;
        }

        private void Apply(bool available, bool emergency, int power, int brake)
        {
            IsAvailable = available;
            if (!available || emergency) power = brake = 0;
            if (powerCells != null)
                foreach (var cell in powerCells)
                {
                    if (cell == null) continue;
                    cell.SetState(cell.Notch == power ? PowerNotchCellState.On :
                        cell.Notch < power ? PowerNotchCellState.Blank : PowerNotchCellState.Off);
                    cell.SetLabelVisible(cell.Notch >= power);
                }
            if (brakeCells != null)
                foreach (var cell in brakeCells)
                {
                    if (cell == null) continue;
                    cell.SetState(available && emergency ? BrakeNotchCellState.Blank :
                        cell.Notch == brake ? BrakeNotchCellState.On :
                        cell.Notch < brake ? BrakeNotchCellState.Blank : BrakeNotchCellState.Off);
                    cell.SetLabelVisible(!(available && emergency) && cell.Notch >= brake);
                }
            if (neutralCell != null)
                neutralCell.SetState(available && !emergency && power == 0 && brake == 0
                    ? NeutralNotchCellState.On : NeutralNotchCellState.Off);
            if (emergencyCell != null)
                emergencyCell.SetState(available && emergency ? EmergencyNotchCellState.On : EmergencyNotchCellState.Off);
            if (unavailableText != null) unavailableText.enabled = false;
        }
    }
}
