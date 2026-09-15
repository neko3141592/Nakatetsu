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
        private bool emergencyLatched;
        private void OnDisable()
        {
            if (TryGetComponent(out TimsCommunicationController communication))
                communication.MasterBus.SetBool(TimsBrakeController.IsEmergencyKey, true);
        }

        public void CalculateAndPublish()
        {
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
            if (consist != null)
            {
                for (int i = 0; i < consist.CarCount; i++)
                {
                    // EB出力は前ステップの値。起動直後の未公開は要求なしとして扱う。
                    if (communication.TryGetLocalBus(i, out TimsBusState local) &&
                        local.TryGetBool(EbDeviceTimsBusSource.IsEmergencyBrakeRequestedKey, out bool eb) && eb)
                        emergency = true;
                }
            }
            emergency |= !traction.isActiveAndEnabled || !brake.isActiveAndEnabled;
            // EB側が5 km/h未満で解除されても停止までは非常を保持する。
            // 原因解消後、停車かつ力行Nで解除できる。
            if (emergency) emergencyLatched = true;
            else if (traction.Context.Input.speedMps < 0.01f && traction.Context.Input.powerNotch == 0)
                emergencyLatched = false;
            emergency |= emergencyLatched;
            brake.Context.Input.canReleaseEmergencyBrake = !emergency;
            traction.Context.Input.isReady = ready && !emergency;
            if (!bus.TryGetInt(TimsDirectionController.ConsistDirectionSignKey, out int sign) || sign == 0)
                traction.Context.Input.powerNotch = 0;

            // 非常フラグは両方のAdapterが優先して読み、力行遮断・最大空気制動にする。
            traction.CalculateAndPublish();
            brake.CalculateAndPublish();
        }

        private static bool CollectInputs(TimsCommunicationController communication,
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
            bi.canReleaseEmergencyBrake = false;
            bi.brakeStep = 0;
            bi.cars.Clear();
            brake.Settings.brakeTargetDecelerationsMps2.Clear();

            if (consist == null || consist.CarCount == 0) return false;
            bool ready = settings != null;
            var bus = communication.MasterBus;
            ready &= TryNonNegative(bus, TimsSpeedController.SpeedMpsKey, out ti.speedMps);
            ready &= bus.TryGetInt(TimsNotchController.ResolvedPowerNotchKey, out ti.powerNotch);
            ready &= bus.TryGetInt(TimsNotchController.ResolvedBrakeStepKey, out bi.brakeStep);
            ti.brakeStep = bi.brakeStep;

            float totalPowerW = 0f;
            for (int i = 0; i < consist.CarCount; i++)
            {
                CarDefinitionAsset definition = consist.cars[i];
                var car = new TimsBrakeCarInput();
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
                        valid &= local.TryGetBool(TimsTractionBusSource.IsAvailableKey, out unit.isAvailable);
                        valid &= local.TryGetInt(TimsTractionBusSource.MotorCountKey, out unit.motorCount) && unit.motorCount > 0;
                        valid &= TryNonNegative(local, TimsTractionBusSource.RatedPowerWKey, out float ratedPowerW);
                        unit.ratedMotorPowerW = unit.motorCount > 0 ? ratedPowerW / unit.motorCount : 0f;
                        unit.hasMotorSettings = definition.motorDefinition != null && ratedPowerW > 0f;
                        if (unit.isAvailable && unit.hasMotorSettings) totalPowerW += ratedPowerW;
                    }
                }
                // 最小版は全車空気制動。回生力の要求は生成しない。
                car.isVvvfMotorCar = false;
                car.isTrailerCar = true;
                ti.units.Add(unit);
                ti.carBCPressuresKPa.Add(car.bcPressureKPa);
                ti.consistMassKg += car.massKg;
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
                        ti.powerStepGain = (float)ti.powerNotch / settings.powerNotchCount;
                    else if (ti.powerNotch <= settings.powerCurves.Length && settings.powerCurves[ti.powerNotch - 1] != null)
                    {
                        float baseSpeed = TimsTractionLogic.CalculateConstantAccelerationEndSpeedMps(
                            totalPowerW, ti.consistMassKg, traction.Settings.launchAccelerationMps2);
                        // 横軸0=停止、1=定加速域の終端。ノッチ配列はP1から。
                        ti.powerStepGain = settings.powerCurves[ti.powerNotch - 1].Evaluate(
                            Mathf.Clamp01(ti.speedMps / Mathf.Max(0.01f, baseSpeed)));
                        ready &= IsNonNegative(ti.powerStepGain);
                    }
                    else ready = false;
                }
            }
            return ready;
        }

        private static bool IsNonNegative(float value) => value >= 0f && !float.IsInfinity(value);
        private static bool TryNonNegative(TimsBusState bus, TimsTagKey key, out float value)
        {
            return bus.TryGetFloat(key, out value) && IsNonNegative(value);
        }
    }
}
