using Nakatetsu.Train.Equipment.Tims;
using Nakatetsu.Train.Equipment.Tims.Brake;
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

        private PowerNotchCell[] powerCells;
        private BrakeNotchCell[] brakeCells;
        private NeutralNotchCell neutralCell;
        private EmergencyNotchCell emergencyCell;

        public bool IsAvailable { get; private set; }

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
            powerCells = GetComponentsInChildren<PowerNotchCell>(true);
            brakeCells = GetComponentsInChildren<BrakeNotchCell>(true);
            neutralCell = GetComponentInChildren<NeutralNotchCell>(true);
            emergencyCell = GetComponentInChildren<EmergencyNotchCell>(true);
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
                    cell.SetState(cell.Notch == brake ? BrakeNotchCellState.On :
                        cell.Notch < brake ? BrakeNotchCellState.Blank : BrakeNotchCellState.Off);
                    cell.SetLabelVisible(cell.Notch >= brake);
                }
            if (neutralCell != null)
                neutralCell.SetState(available && !emergency && power == 0 && brake == 0
                    ? NeutralNotchCellState.On : NeutralNotchCellState.Off);
            if (emergencyCell != null)
                emergencyCell.SetState(available && emergency ? EmergencyNotchCellState.On : EmergencyNotchCellState.Off);
            if (unavailableText != null) unavailableText.enabled = !available;
        }
    }
}
