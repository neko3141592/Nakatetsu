using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Configuration;
using Nakatetsu.Train.Equipment.Tims.Notch;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Equipment.Tims.Safety.Eb;
using Nakatetsu.Train.Equipment.Tims.Speed;
using Nakatetsu.Train.Equipment.Tims.Traction;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims
{
    // 通信の収集後、装置の入力収集前に1回実行する最小のTIMS指令パイプライン。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TimsRoot), typeof(TimsCommunicationController), typeof(TimsDirectionController))]
    [RequireComponent(typeof(TimsNotchController), typeof(TimsTractionController), typeof(TimsBrakeController))]
    public sealed class TimsControlController : MonoBehaviour
    {
        private readonly List<string> emergencyReasons = new();
        private string inputLocation;
        public string EmergencyReason { get; private set; } = "制御未実行（Simulation・通信の実行状態を確認）";
        private void OnDisable()
        {
            if (TryGetComponent(out TimsCommunicationController communication))
                communication.MasterBus.SetBool(TimsBrakeController.IsEmergencyKey, true);
        }

        public void CalculateAndPublish()
        {
            emergencyReasons.Clear();
            var communication = GetComponent<TimsCommunicationController>();
            var direction = GetComponent<TimsDirectionController>();
            var notch = GetComponent<TimsNotchController>();
            var traction = GetComponent<TimsTractionController>();
            var brake = GetComponent<TimsBrakeController>();
            var train = GetComponentInParent<TrainRoot>(true);
            var settings = GetComponent<TimsRoot>().Settings;
            var consist = train != null ? train.ConsistDefinition : null;
            var bus = communication.MasterBus;

            bool directionReady = direction.isActiveAndEnabled && direction.CalculateAndPublish();
            if (!directionReady)
            {
                bus.SetInt(TimsDirectionController.ActivatedCabPositionKey, 0);
                bus.SetInt(TimsDirectionController.ConsistDirectionSignKey, 0);
            }
            notch.CollectInput();
            notch.CalculateAndPublish();

            // 同じ測定スナップショットから両方のContextを収集する。
            bool ready = CollectInputs(communication, consist, settings, traction.Context, brake.Context);
            bool emergency = !ready || !directionReady || !notch.isActiveAndEnabled ||
                !notch.Context.Input.isReady || notch.Output.isEmergencyBrakeRequested;
            if (!ready && emergencyReasons.Count == 0) emergencyReasons.Add("力行・ブレーキ入力が無効");
            if (!directionReady) emergencyReasons.Add("Directionの設定・入力不足または無効化");
            if (!notch.isActiveAndEnabled) emergencyReasons.Add("Notchが無効");
            if (!notch.Context.Input.isReady) emergencyReasons.Add("有効運転台またはマスコン入力なし");
            if (notch.Output.isEmergencyBrakeRequested) emergencyReasons.Add("Notch非常要求（運転台未選択・非常ノッチ等）");
            if (consist != null)
            {
                for (int i = 0; i < consist.CarCount; i++)
                {
                    // EB出力は前ステップの値。起動直後の未公開は要求なしとして扱う。
                    if (communication.TryGetLocalBus(i, out TimsBusState local) &&
                        local.TryGetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, out bool eb) && eb)
                    {
                        emergency = true;
                        emergencyReasons.Add($"車両{i + 1}: EB要求");
                    }
                }
            }
            emergency |= !traction.isActiveAndEnabled || !brake.isActiveAndEnabled;
            if (!traction.isActiveAndEnabled || !brake.isActiveAndEnabled)
                emergencyReasons.Add("TractionまたはBrakeが無効");
            // TIMSは現在の要求だけを集約する。保持・解除は要求元の装置が判断する。
            EmergencyReason = emergency ? string.Join("\n", emergencyReasons) : "なし";
            brake.Context.Input.isEmergencyBrakeRequested = emergency;
            traction.Context.Input.isReady = ready && !emergency;
            if (!bus.TryGetInt(TimsDirectionController.ConsistDirectionSignKey, out int sign) || sign == 0)
                traction.Context.Input.powerNotch = 0;

            // ドア監視搭載編成だけに適用。表示部品には依存せず集約した接点を使用する。
            if (TryGetComponent(out TimsDoorController doors) &&
                (!doors.isActiveAndEnabled ||
                 !bus.TryGetBool(TimsDoorController.HasValidStateKey, out bool validDoors) || !validDoors ||
                 !bus.TryGetBool(TimsDoorController.TractionPermittedKey, out bool closedDoors) || !closedDoors))
                traction.Context.Input.powerNotch = 0;

            // 非常フラグは両方のAdapterが優先して読み、力行遮断・最大空気制動にする。
            traction.CalculateAndPublish();
            brake.CalculateAndPublish();
        }

        private bool CollectInputs(TimsCommunicationController communication,
            ConsistDefinitionAsset consist, TimsSettingsAsset settings,
            TimsTractionContext traction, TimsBrakeContext brake)
        {
            var ti = traction.Input;
            var bi = brake.Input;
            ti.isReady = false;
            ti.speedMps = 0f;
            ti.consistMassKg = 0f;
            ti.powerNotch = 0;
            ti.brakeStep = 0;
            ti.powerStepGain = 0f;
            ti.isGradientStart = false;
            ti.currentBCPressureKPa = 0f;
            ti.units.Clear();
            ti.carBCPressuresKPa.Clear();
            bi.isEmergencyBrakeRequested = true;
            bi.brakeStep = 0;
            bi.cars.Clear();
            brake.Settings.brakeTargetDecelerationsMps2.Clear();

            if (consist == null || consist.CarCount == 0)
            { emergencyReasons.Add("編成定義が未設定または車両数0"); return false; }
            inputLocation = "編成";
            bool ready = settings != null;
            if (settings == null) emergencyReasons.Add("TIMS Settings未設定");
            var bus = communication.MasterBus;
            ready &= TryNonNegative(bus, TimsSpeedController.SpeedMpsKey, out ti.speedMps);
            ready &= bus.TryGetInt(TimsNotchController.ResolvedPowerNotchKey, out ti.powerNotch);
            ready &= bus.TryGetInt(TimsNotchController.ResolvedBrakeStepKey, out bi.brakeStep);
            ti.brakeStep = bi.brakeStep;

            for (int i = 0; i < consist.CarCount; i++)
            {
                inputLocation = $"車両{i + 1}";
                CarDefinitionAsset definition = consist.cars[i];
                var car = new TimsBrakeCarInput
                {
                    isTrailerCar = definition != null && definition.motorCount == 0
                };
                var unit = new TimsTractionUnitInput();
                bi.cars.Add(car);
                bool hasLocal = communication.TryGetLocalBus(i, out TimsBusState local);
                bool valid = definition != null && hasLocal;
                // 入力欠損時にも、定義上の能力を非常制動のために残す。
                if (definition != null && definition.brakeCylinderDefinition != null)
                {
                    var cylinder = definition.brakeCylinderDefinition.Settings;
                    car.maxBCPressureKPa = cylinder.maximumPressureKPa;
                    car.airForcePerKPa = cylinder.pistonAreaM2 * 1000f * cylinder.mechanicalEfficiency * definition.brakeCylinderCount;
                    car.airCapN = car.maxBCPressureKPa * car.airForcePerKPa;
                }
                else valid = false;
                if (hasLocal)
                {
                    valid &= TryNonNegative(local, BrakeControlDeviceTimsBusSource.MassKgKey, out car.massKg) && car.massKg > 0f;
                    valid &= TryNonNegative(local, BrakeControlDeviceTimsBusSource.PressureKPaKey, out car.bcPressureKPa);
                    valid &= TryNonNegative(local, BrakeControlDeviceTimsBusSource.ActualForceNKey, out car.airForceN);
                    bool hasCapacity = TryNonNegative(local, BrakeControlDeviceTimsBusSource.ForcePerKPaKey, out float forcePerKPa);
                    hasCapacity &= TryNonNegative(local, BrakeControlDeviceTimsBusSource.MaximumPressureKPaKey, out float maxPressure);
                    if (hasCapacity)
                    {
                        car.airForcePerKPa = forcePerKPa;
                        car.maxBCPressureKPa = maxPressure;
                        car.airCapN = forcePerKPa * maxPressure;
                    }
                    valid &= hasCapacity && car.airCapN > 0f;
                    if (definition != null && definition.motorCount > 0)
                    {
                        valid &= local.TryGetBool(TimsTractionBusSource.IsVvvfMotorCarKey, out car.isVvvfMotorCar);
                        valid &= TryNonNegative(local, TimsTractionBusSource.RegenCapacityNKey, out car.regenCapN);
                        valid &= TryNonNegative(local, TimsTractionBusSource.ActualRegenForceNKey, out car.regenForceN);
                        valid &= local.TryGetBool(TimsTractionBusSource.IsAvailableKey, out unit.isAvailable);
                        if (!unit.isAvailable) car.regenCapN = 0f;
                        valid &= local.TryGetInt(TimsTractionBusSource.MotorCountKey, out unit.motorCount) && unit.motorCount > 0;
                        valid &= TryNonNegative(local, TimsTractionBusSource.RatedPowerWKey, out float ratedPowerW);
                        unit.ratedMotorPowerW = unit.motorCount > 0 ? ratedPowerW / unit.motorCount : 0f;
                        unit.hasMotorSettings = definition.motorDefinition != null && ratedPowerW > 0f;
                    }
                }
                ti.units.Add(unit);
                ti.carBCPressuresKPa.Add(car.bcPressureKPa);
                ti.consistMassKg += car.massKg;
                if (!valid && definition != null && definition.emptyMassKg <= 0f && car.massKg <= 0f)
                    emergencyReasons.Add($"{inputLocation}: {definition.name}の空車質量が0以下、測定質量も無効");
                if (!valid) emergencyReasons.Add($"{inputLocation}: 質量・制動能力・機器定義・LocalBus／VVVF情報のいずれかが無効");
                ready &= valid;
            }
            if (settings != null)
            {
                traction.Settings.launchAccelerationMps2 = settings.launchAccelerationKmhPerSec / 3.6f;
                brake.Settings.brakeSubstepCount = settings.brakeSubstepCount;
                brake.Settings.minimumServiceBrakePressureKPa = settings.minimumServiceBrakePressureKPa;
                brake.Settings.minimumServiceBrakePressureLoadScaleMax = settings.minimumServiceBrakePressureLoadScaleMax;
                ready &= settings.brakeSubstepCount > 0 && settings.brakeTargetDecelerationsKmhPerSec.Count == settings.brakeNotchCount;
                foreach (float value in settings.brakeTargetDecelerationsKmhPerSec)
                {
                    ready &= IsNonNegative(value);
                    brake.Settings.brakeTargetDecelerationsMps2.Add(value / 3.6f);
                }
                ready &= ti.powerNotch >= 0 && ti.powerNotch <= settings.powerNotchCount && bi.brakeStep >= 0;
                if (ti.powerNotch > 0 && settings.powerNotchCount > 0)
                {
                    if (settings.powerCurves == null || settings.powerCurves.Length == 0)
                    {
                        ti.powerStepGain = (float)ti.powerNotch / settings.powerNotchCount;
                    }
                    else if (ti.powerNotch <= settings.powerCurves.Length &&
                        settings.powerCurves[ti.powerNotch - 1] != null &&
                        IsNonNegative(settings.maximumOperatingSpeedKmh) && settings.maximumOperatingSpeedKmh > 0f)
                    {
                        // 横軸0=停止、1=設定した最高運転速度。ノッチ配列はP1から。
                        float normalizedSpeed = Mathf.Clamp01(ti.speedMps * 3.6f / settings.maximumOperatingSpeedKmh);
                        ti.powerStepGain = settings.powerCurves[ti.powerNotch - 1].Evaluate(normalizedSpeed);
                        ready &= IsNonNegative(ti.powerStepGain);
                    }
                    else
                    {
                        ready = false;
                    }
                }
            }
            if (!ready && settings != null && emergencyReasons.Count == 0)
                emergencyReasons.Add("ノッチ段数・減速度表・力行カーブの設定が無効");
            return ready;
        }

        private static bool IsNonNegative(float value) => value >= 0f && !float.IsInfinity(value);
        private bool TryNonNegative(TimsBusState bus, TimsTagKey key, out float value)
        {
            bool valid = bus.TryGetFloat(key, out value) && IsNonNegative(value);
            if (!valid) emergencyReasons.Add($"{inputLocation}: {key}が未受信または不正");
            return valid;
        }
    }
}
