using System;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Atc
{
    public static class TrainAtcLogic
    {
        private const float EmergencySpeedMarginKmh = 10f;

        public static bool TryInitializePosition(
            TrainAtcContext context,
            TrackAtcGraphDefinition graph,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            return TrainAtcPositionLogic.TryInitializePosition(context, graph, frontPosition, rearPosition);
        }

        public static bool TryCorrectPosition(
            TrainAtcContext context,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            return TrainAtcPositionLogic.TryCorrectPosition(context, frontPosition, rearPosition);
        }

        public static bool TryGetPathPointInformation(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            float distanceOnPathM,
            out TrainAtcPathPointInformation information)
        {
            return TrainAtcBrakePatternLogic.TryGetPathPointInformation(context, routeInfomation,
                distanceOnPathM, out information);
        }

        public static bool TryGetPathDistance(
            TrainAtcContext context,
            TrackCircuitAtcRouteInfomation routeInfomation,
            string atcEdgeId,
            float distanceOnAtcEdgeM,
            out float distanceOnPathM)
        {
            return TrainAtcBrakePatternLogic.TryGetPathDistance(context, routeInfomation,
                atcEdgeId, distanceOnAtcEdgeM, out distanceOnPathM);
        }

        public static void Calculate(TrainAtcContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.State;

            // 正常判定は毎回やり直す。確認に失敗した処理がisHealthyをfalseにする。
            state.isHealthy = true;
            state.isAtcPowerOn = false;
            state.isAtcEnabled = false;

            CaptureCabState(context);
            CaptureCurrentTelegram(context);
            var retainedPattern = GetRetainedBrakePattern(context);

            // キー切・有効運転台なしでも、両端の保持位置は測定速度から更新する。
            TrainAtcPositionLogic.UpdateAtcEdgePosition(context, retainedPattern);
            TrainAtcPositionLogic.CaptureCurrentPosition(context);

            state.isAtcEnabled = state.isHealthy && state.isAtcPowerOn &&
                state.cab.reverserPosition != ReverserPosition.Neutral;

            // 更新前に、入力・電文・猶予を確認して生成・保持・消去を決める。
            state.brakePatternUpdateDecision = CheckBrakePatternInput(context, retainedPattern);
            TrainAtcBrakePatternLogic.UpdateBrakePattern(context, EmergencySpeedMarginKmh);

            var patternUpdate = state.brakePatternUpdateDecision;
            bool hasValidPattern = patternUpdate.mode == TrainAtcBrakePatternUpdateMode.Retain;
            float distanceOnPathM = patternUpdate.distanceOnPathM;
            int sampleIndex = patternUpdate.sampleIndex;
            float ratio = patternUpdate.ratio;
            if (patternUpdate.mode == TrainAtcBrakePatternUpdateMode.Create)
            {
                // 生成後に現在位置のサンプルを取得する。入力・電文の判定は繰り返さない。
                hasValidPattern = TrainAtcBrakePatternLogic.TryGetBrakePatternPosition(
                    context, out distanceOnPathM, out sampleIndex, out ratio);
                if (hasValidPattern)
                {
                    state.noSignalElapsedSeconds = 0f;
                }
                else
                {
                    TrainAtcBrakePatternLogic.ClearBrakePattern(context);
                }
            }
            TrainAtcBrakePatternLogic.UpdateCurrentPattern(
                context, hasValidPattern, distanceOnPathM, sampleIndex, ratio);
            int baseBrakeStep = GetBaseBrakeStep(context, sampleIndex);
            // 無効でも、確認済みのキー切・中立による解除と、取得失敗による非常を判定する。
            TrainAtcBrakeLogic.UpdateBrakeState(context, baseBrakeStep);
            // 位置・パターン・ブレーキの状態が確定してから、外部へ渡す出力を生成する。
            TrainAtcOutputLogic.UpdateOutput(context);
        }

        private static void CaptureCabState(TrainAtcContext context)
        {
            var input = context.Input;
            var state = context.State;
            // 操作状態を収集時点の値として保持する。取得できなければ前回値を残さない。
            state.hasCabState = false;
            state.cab = default;

            if (!input.hasCabState)
            {
                state.isHealthy = false;
                return;
            }

            var cab = input.cab;
            if (cab.carIndex < 0 ||
                (cab.isFrontCab && cab.carIndex != 0) ||
                cab.powerPosition < 0 || cab.brakePosition < 0 || cab.serviceBrakePosition < 0 ||
                cab.serviceBrakePosition > cab.brakePosition ||
                (cab.powerPosition > 0 && cab.brakePosition > 0) ||
                cab.reverserPosition < ReverserPosition.Reverse ||
                cab.reverserPosition > ReverserPosition.Forward)
            {
                state.isHealthy = false;
                return;
            }

            state.hasCabState = true;
            state.cab = input.cab;
            state.isAtcPowerOn = cab.isKeyInserted;
        }

        private static void CaptureCurrentTelegram(TrainAtcContext context)
        {
            var input = context.Input;
            var state = context.State;
            state.currentTelegram = null;
            if (!state.hasCabState)
            {
                return;
            }

            // 有効運転台側だけを選ぶ。未受信時に反対側や前回の電文を使用しない。
            if (state.cab.isFrontCab)
            {
                state.currentTelegram = input.frontTelegram?.Clone();
            }
            else
            {
                state.currentTelegram = input.rearTelegram?.Clone();
            }
        }

        private static TrainAtcBrakePattern GetRetainedBrakePattern(TrainAtcContext context)
        {
            var state = context.State;
            var pattern = state.brakePattern;
            // 運転台・レバーサを切り替えた場合は、前回の経路を引き継がない。
            if (!state.hasCabState || !state.isAtcPowerOn ||
                state.cab.reverserPosition == ReverserPosition.Neutral ||
                pattern.isFrontCab != state.cab.isFrontCab ||
                pattern.reverserPosition != state.cab.reverserPosition || pattern.normalPattern.Count < 2)
            {
                return null;
            }
            return pattern;
        }

        private static TrainAtcBrakePatternUpdateDecision CheckBrakePatternInput(
            TrainAtcContext context, TrainAtcBrakePattern retainedPattern)
        {
            var decision = new TrainAtcBrakePatternUpdateDecision();
            var state = context.State;
            if (!state.isAtcEnabled)
            {
                state.noSignalElapsedSeconds = 0f;
                return decision;
            }

            // 計算用入力・設定の不正は、無信号中も即時に不正とする。
            if (!TryGetBrakePatternInputs(context, out decision.totalMassKg, out decision.receiverDistanceFromFrontM))
            {
                return decision;
            }

            float timeoutSeconds = context.Settings.noSignalTimeoutSeconds;
            var telegram = state.currentTelegram;
            if (telegram != null && telegram.isValid &&
                telegram.atcRouteInfomation.TryGetValue(
                    (state.currentPosition.atcEdgeId, state.currentTravelDirection), out decision.routeInfomation))
            {
                var route = decision.routeInfomation;
                if (route == null || route.atcEdgePath == null || route.atcEdgePath.Count == 0 ||
                    route.atcEdgePath[0] != state.currentPosition.atcEdgeId ||
                    route.overrunProtectionMode < OverrunProtectionMode.None ||
                    route.overrunProtectionMode > OverrunProtectionMode.Restricted)
                {
                    return decision;
                }
                decision.mode = TrainAtcBrakePatternUpdateMode.Create;
                return decision;
            }

            state.noSignalElapsedSeconds += context.Input.deltaTimeSeconds;
            if (retainedPattern == null || timeoutSeconds == 0f || state.noSignalElapsedSeconds > timeoutSeconds)
            {
                return decision;
            }

            // 保持可能かも更新前に確認する。表示とブレーキはこの位置情報を共有する。
            if (!TrainAtcBrakePatternLogic.TryGetBrakePatternPosition(context,
                out decision.distanceOnPathM, out decision.sampleIndex, out decision.ratio))
            {
                return decision;
            }

            decision.mode = TrainAtcBrakePatternUpdateMode.Retain;
            return decision;
        }

        private static bool TryGetBrakePatternInputs(
            TrainAtcContext context, out double totalMassKg, out float receiverDistanceFromFrontM)
        {
            totalMassKg = 0d;
            receiverDistanceFromFrontM = 0f;
            var settings = context.Settings;
            var input = context.Input;
            if (!input.hasBrakeSettings || !input.hasSpeedMeasurement ||
                float.IsNaN(input.signedSpeedMps) || float.IsInfinity(input.signedSpeedMps) ||
                !IsNonNegativeFinite(settings.noSignalTimeoutSeconds) ||
                !IsNonNegativeFinite(context.State.noSignalElapsedSeconds) ||
                !IsPositiveFinite(settings.maximumSamplingIntervalM) ||
                !IsNonNegativeFinite(settings.MaximumOperatingSpeedMps) ||
                !IsNonNegativeFinite((settings.maximumOperatingSpeedKmh + EmergencySpeedMarginKmh) / 3.6f) ||
                !IsNonNegativeFinite(settings.patternApproachWarningTimeSeconds) ||
                !IsPositiveFinite(settings.serviceDecelerationMps2) ||
                !IsPositiveFinite(settings.emergencyDecelerationMps2) ||
                !IsNonNegativeFinite(settings.maximumDownhillGradientPermille) ||
                !input.hasCarMasses || input.cars.Count == 0)
            {
                return false;
            }

            // 質量と車両中心位置は、編成定義とTIMSから取り込んだ値だけを使う。
            float previousCenterDistanceM = -1f;
            foreach (var car in input.cars)
            {
                if (!IsPositiveFinite(car.massKg) || !IsNonNegativeFinite(car.centerDistanceFromFrontM) ||
                    car.centerDistanceFromFrontM <= previousCenterDistanceM)
                {
                    return false;
                }
                totalMassKg += car.massKg;
                previousCenterDistanceM = car.centerDistanceFromFrontM;
            }

            receiverDistanceFromFrontM = input.frontReceiverDistanceFromFrontM;
            if (!context.State.cab.isFrontCab)
            {
                receiverDistanceFromFrontM = input.rearReceiverDistanceFromFrontM;
            }
            return !float.IsNaN(receiverDistanceFromFrontM) && !float.IsInfinity(receiverDistanceFromFrontM);
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static int GetBaseBrakeStep(TrainAtcContext context, int sampleIndex)
        {
            var pattern = context.State.brakePattern;
            if (!pattern.hasValidPattern || !pattern.isNormalDecelerationSection)
            {
                return -1;
            }

            // 基準段はLogicで求め、ブレーキ判定へ渡す。取得失敗は-1とする。
            float decelerationMps2 = pattern.normalPattern[sampleIndex].decelerationMps2;
            if (!TrainAtcNotchHelper.TryGetNearestBrakeStep(
                decelerationMps2, context.Input.brakeSettings, out int brakeStep))
            {
                return -1;
            }
            return brakeStep;
        }
    }
}
