using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcValidationLogic
    {
        internal static void UpdateValidation(TrainAtcContext context, bool hasReceiverChanged)
        {
            var validation = context.State.validation;

            // 今回の判定結果を作る。タイマーだけは前回の無信号時間を引き継ぐ。
            validation.result = TrainAtcValidationResult.Unusable;
            validation.routeInfomation = null;
            validation.totalMassKg = 0d;
            validation.receiverDistanceFromFrontM = 0f;
            validation.isInputValid = TryValidateInputs(context);

            // 入力不正でも受信回復を独立して確認し、読める場合は必ずタイマーを戻す。
            bool hasRoute = TryReadCurrentRoute(context, out var route);
            UpdateNoSignalTime(context, hasRoute);

            if (!validation.isInputValid || !context.State.operation.isAtcEnabled)
            {
                return;
            }

            if (hasRoute)
            {
                validation.routeInfomation = route;
                validation.result = TrainAtcValidationResult.Adopt;
                return;
            }

            // 勾配補正の基準点が変わるため、受電器を切り替えたtickでは旧パターンを保持しない。
            if (!hasReceiverChanged && CanRetainPattern(context))
            {
                validation.result = TrainAtcValidationResult.Retain;
            }
        }

        internal static void ValidateProtectionSettings(TrainAtcContext context)
        {
            var validation = context.State.validation;
            var protection = context.State.protectionMode;
            if (!validation.isInputValid || !context.State.operation.isAtcEnabled ||
                !protection.isProtectionModeKnown)
            {
                return;
            }

            // 受信値ではなく、ORP保持・解除を反映して使用する方式の設定を確認する。
            if (!TrainAtcValidationHelper.AreProtectionSettingsValid(
                    context.Settings, protection.overrunProtectionMode))
            {
                validation.isInputValid = false;
                validation.result = TrainAtcValidationResult.Unusable;
                validation.routeInfomation = null;
            }
        }

        private static bool TryValidateInputs(TrainAtcContext context)
        {
            var input = context.Input;
            var operation = context.State.operation;
            var position = context.State.position;
            var validation = context.State.validation;

            // 工程2・3の確認結果を利用し、計算に必要な基本入力をここにまとめる。
            if (!operation.hasCabState || !position.isPositionInitialized || !position.isPositionKnown ||
                context.Graph == null || !input.hasSpeedMeasurement ||
                !TrainAtcValidationHelper.IsFinite(input.signedSpeedMps) ||
                !TrainAtcValidationHelper.IsNonNegativeFinite(input.deltaTimeSeconds) ||
                !input.hasCarMasses || !input.hasBrakeSettings ||
                !TrainAtcValidationHelper.AreSettingsValid(context.Settings) ||
                !TrainAtcValidationHelper.AreBrakeSettingsValid(input.brakeSettings) ||
                !TrainAtcValidationHelper.IsNonNegativeFinite(validation.noSignalElapsedSeconds))
            {
                return false;
            }

            if (!TrainAtcValidationHelper.TryGetTotalMass(input.cars, out double totalMassKg))
            {
                return false;
            }

            // キー切・中立でも、取得できた入力の正常性は確認する。電文は要求しない。
            if (!operation.isAtcEnabled)
            {
                validation.totalMassKg = totalMassKg;
                return true;
            }

            if (!operation.hasCurrentPosition || operation.currentPosition == null ||
                operation.currentTravelDirection == TrackAtcTravelDirection.Unspecified)
            {
                return false;
            }

            float receiverDistanceM = input.frontReceiverDistanceFromFrontM;
            if (operation.selectedReceiver == TrainAtcReceiverSide.Rear)
            {
                receiverDistanceM = input.rearReceiverDistanceFromFrontM;
            }
            if (!TrainAtcValidationHelper.IsFinite(receiverDistanceM))
            {
                return false;
            }

            validation.totalMassKg = totalMassKg;
            validation.receiverDistanceFromFrontM = receiverDistanceM;
            return true;
        }

        private static bool TryReadCurrentRoute(
            TrainAtcContext context, out TrackCircuitAtcRouteInfomation route)
        {
            route = null;
            var operation = context.State.operation;
            var position = operation.currentPosition;
            var telegram = operation.currentTelegram;
            if (position == null || telegram == null || !telegram.isValid ||
                !context.atcEdgesById.TryGetValue(position.atcEdgeId, out var edge) ||
                string.IsNullOrEmpty(edge.trackCircuitId))
            {
                return false;
            }

            // 電文には軌道回路IDがないため、現在Edgeと照査方向のキーで対応を確認する。
            // 接続不正や方式不正は、読めた後の工程5・6で判定する。
            return telegram.atcRouteInfomation.TryGetValue(
                (position.atcEdgeId, operation.currentTravelDirection), out route) && route != null;
        }

        private static void UpdateNoSignalTime(TrainAtcContext context, bool hasRoute)
        {
            var validation = context.State.validation;
            var operation = context.State.operation;

            // 確認済みのキー切・中立では受電器を選択していないので、無信号を積算しない。
            validation.isNoSignal = operation.isAtcEnabled && !hasRoute;
            if (!validation.isNoSignal)
            {
                validation.noSignalElapsedSeconds = 0f;
                return;
            }

            float deltaTimeSeconds = context.Input.deltaTimeSeconds;
            if (TrainAtcValidationHelper.IsNonNegativeFinite(deltaTimeSeconds))
            {
                // floatの上限へ収め、長時間の無信号でInfinityにしない。
                double elapsedSeconds = (double)validation.noSignalElapsedSeconds + deltaTimeSeconds;
                validation.noSignalElapsedSeconds = (float)System.Math.Min(float.MaxValue, elapsedSeconds);
            }
        }

        private static bool CanRetainPattern(TrainAtcContext context)
        {
            var validation = context.State.validation;
            var pattern = context.State.pattern;
            var operation = context.State.operation;
            float timeoutSeconds = context.Settings.noSignalTimeoutSeconds;
            if (timeoutSeconds <= 0f || validation.noSignalElapsedSeconds > timeoutSeconds ||
                !pattern.isValid || !context.State.protectionMode.isProtectionModeKnown ||
                pattern.normalPattern.samples.Count < 2 || pattern.emergencyPattern.samples.Count < 2)
            {
                return false;
            }

            // 前回経路上にいるだけでなく、その地点の進行方向も一致する場合だけ保持する。
            return TrainAtcValidationHelper.IsPositionOnRetainedPath(
                context.atcEdgesById, pattern.atcEdgePath, pattern.pathStartTravelDirection,
                operation.currentPosition.atcEdgeId, operation.currentPosition.distanceOnAtcEdgeM,
                operation.currentTravelDirection);
        }
    }
}
