using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Integration;
using Nakatetsu.Train.Equipment.Tims.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Notch
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsCommunicationController))]
    public sealed class TimsNotchController : MonoBehaviour
    {
        public static readonly TimsTagKey IsEmergencyBrakeRequestedKey =
            new("Notch", "IsEmergencyBrakeRequested");
        public static readonly TimsTagKey ResolvedPowerNotchKey =
            new("Notch", "ResolvedPowerNotch");
        public static readonly TimsTagKey ResolvedBrakeStepKey =
            new("Notch", "ResolvedBrakeStep");
        public static readonly TimsTagKey ManualBrakeStepLabelKey =
            new("Notch", "ManualBrakeStepLabel");
        public static readonly TimsTagKey BrakeStepLabelKey =
            new("Notch", "BrakeStepLabel");
        public static readonly TimsTagKey ResolvedNotchLabelKey =
            new("Notch", "ResolvedNotchLabel");

        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private TimsRoot timsRoot;
        [SerializeField] private TimsCommunicationController communicationController;

        private readonly TimsNotchContext context = new();

        public TimsNotchContext Context => context;
        public TimsNotchOutput Output => context.Output;

        private void Awake()
        {
            // 編成ルート、TIMSルート、通信Controllerを解決する。
            ResolveReferences();
        }

        public void CollectInput()
        {
            // MasterBusの有効運転台と各車LocalBusのマスコン状態を収集する。
            TimsNotchInput input = context.Input;
            input.isReady = false;
            input.cabActivationPosition = CabActivationPosition.Off;

            if (!ResolveReferences() ||
                trainRoot.ConsistDefinition == null ||
                !CollectSettings())
            {
                return;
            }

            int carCount = trainRoot.ConsistDefinition.CarCount;
            if (!TryReadActivatedCabPosition(out CabActivationPosition activatedCabPosition))
            {
                FillCarInputs(input, carCount, -1, out _);
                return;
            }

            input.cabActivationPosition = activatedCabPosition;
            int activatedCabIndex = activatedCabPosition switch
            {
                CabActivationPosition.Front => 0,
                CabActivationPosition.Rear => carCount - 1,
                _ => -1
            };
            FillCarInputs(input, carCount, activatedCabIndex, out bool hasActivatedCabInput);

            input.isReady = activatedCabIndex >= 0 && hasActivatedCabInput;
        }

        private void FillCarInputs(
            TimsNotchInput input,
            int carCount,
            int activatedCabIndex,
            out bool hasActivatedCabInput)
        {
            // 編成順にLocalBusを読み、入力配列の位置をcarIndexと一致させる。
            hasActivatedCabInput = false;
            ResizeCarInputs(input, carCount);

            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                bool hasCarInput = TryReadCarInput(
                    carIndex,
                    out int powerNotch,
                    out int brakeNotch);

                if (hasCarInput)
                {
                    TimsCabInput carInput = input.carInputs[carIndex] ?? new TimsCabInput();
                    carInput.powerNotch = powerNotch;
                    carInput.brakeNotch = brakeNotch;
                    input.carInputs[carIndex] = carInput;
                }
                else
                {
                    input.carInputs[carIndex] = null;
                }

                if (carIndex == activatedCabIndex)
                {
                    hasActivatedCabInput = hasCarInput;
                }
            }
        }

        private static void ResizeCarInputs(TimsNotchInput input, int carCount)
        {
            // 車両数に合わせて入力リストの要素数を調整する。
            while (input.carInputs.Count < carCount)
            {
                input.carInputs.Add(null);
            }

            if (input.carInputs.Count > carCount)
            {
                input.carInputs.RemoveRange(carCount, input.carInputs.Count - carCount);
            }
        }

        private bool TryReadActivatedCabPosition(out CabActivationPosition position)
        {
            // MasterBusから編成全体で決定済みの有効運転台を読み取る。
            position = CabActivationPosition.Off;
            if (!communicationController.MasterBus.TryGetInt(
                    TimsDirectionController.ActivatedCabPositionKey,
                    out int rawPosition) ||
                rawPosition < (int)CabActivationPosition.Rear ||
                rawPosition > (int)CabActivationPosition.Front)
            {
                return false;
            }

            position = (CabActivationPosition)rawPosition;
            return true;
        }

        private bool TryReadCarInput(
            int carIndex,
            out int powerNotch,
            out int brakeNotch)
        {
            // 指定車両のLocalBusから力行・ブレーキ位置を読み取る。
            powerNotch = 0;
            brakeNotch = 0;
            if (!communicationController.TryGetLocalBus(carIndex, out TimsBusState localBus) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.PowerPositionKey,
                    out powerNotch) ||
                !localBus.TryGetInt(
                    MasterControllerTimsBusSource.BrakePositionKey,
                    out brakeNotch))
            {
                return false;
            }

            return true;
        }

        public bool CalculateAndPublish()
        {
            // 入力収集後の計算結果をMasterBusへ公開する。
            ResolveReferences();
            if (communicationController == null)
            {
                return false;
            }

            TimsNotchLogic.Calculate(context);
            PublishToMasterBus(communicationController.MasterBus);
            return true;
        }

        private void PublishToMasterBus(TimsBusState masterBus)
        {
            TimsNotchOutput output = context.Output;
            masterBus.SetBool(IsEmergencyBrakeRequestedKey, output.isEmergencyBrakeRequested);
            masterBus.SetInt(ResolvedPowerNotchKey, output.resolvedPowerNotch);
            masterBus.SetInt(ResolvedBrakeStepKey, output.resolvedBrakeStep);
            masterBus.SetString(ManualBrakeStepLabelKey, output.manualBrakeStepLabel);
            masterBus.SetString(BrakeStepLabelKey, output.brakeStepLabel);
            masterBus.SetString(ResolvedNotchLabelKey, output.resolvedNotchLabel);
        }

        private bool CollectSettings()
        {
            // TIMSルートの設定アセットからノッチ計算用Settingsを収集する。
            if (timsRoot.Settings == null)
            {
                return false;
            }

            TimsNotchSettings target = context.Settings;
            target.powerNotchCount = Mathf.Max(1, timsRoot.Settings.powerNotchCount);
            target.brakeNotchCount = Mathf.Max(1, timsRoot.Settings.brakeNotchCount);
            target.masterControllerEmergencyBrakeNotchPosition = Mathf.Max(
                target.brakeNotchCount + 1,
                timsRoot.Settings.masterControllerEmergencyBrakeNotchPosition);
            target.brakeSubstepCount = Mathf.Max(1, timsRoot.Settings.brakeSubstepCount);
            return true;
        }

        private bool ResolveReferences()
        {
            // 親階層の編成ルート、TIMSルートと通信Controllerを取得する。
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            if (timsRoot == null)
            {
                timsRoot = GetComponentInParent<TimsRoot>(true);
            }

            if (communicationController == null)
            {
                communicationController = GetComponent<TimsCommunicationController>();
            }

            return trainRoot != null && timsRoot != null && communicationController != null;
        }
    }
}
