using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;

namespace Nakatetsu.Train.Equipment.Atc
{
    public static class TrainAtcLogic
    {
        public static void Calculate(TrainAtcContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // 工程1のスナップショットはControllerで完了している。
            TrainAtcPositionLogic.UpdatePosition(context);
            var previousReceiver = context.State.operation.selectedReceiver;
            TrainAtcOperationLogic.UpdateOperation(context);
            bool hasReceiverChanged = previousReceiver != context.State.operation.selectedReceiver;

            // 入力・受信判定を終えてから、防護方式とパターンを更新する。
            TrainAtcValidationLogic.UpdateValidation(context, hasReceiverChanged);
            TrainAtcProtectionModeLogic.UpdateProtectionMode(context);
            TrainAtcValidationLogic.ValidateProtectionSettings(context);
            bool hasValidPattern = TrainAtcPatternLogic.UpdatePattern(context);

            // 全体の正常性だけを取りまとめる。子Stateの結果は書き換えない。
            var state = context.State;
            state.isAtcHealthy = state.validation.isInputValid &&
                (!state.operation.isAtcEnabled ||
                 (state.protectionMode.isProtectionModeKnown && hasValidPattern));

            // 前工程が使用不可でも、今回の要求・非常保持・解除を必ず更新する。
            TrainAtcBrakeLogic.UpdateBrakeState(context);

            // 工程8。各工程の確定結果から外部向けOutputを生成する。
            TrainAtcOutputLogic.UpdateAtcOutput(context);
        }

        public static bool TryInitializePosition(
            TrainAtcContext context,
            TrackAtcGraphDefinition graph,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            if (context == null || graph == null || context.State.position.isPositionInitialized ||
                !TryCreateEdgeDictionary(graph, out var edgesById))
            {
                return false;
            }

            context.Graph = graph;
            context.atcEdgesById.Clear();
            foreach (var pair in edgesById)
            {
                context.atcEdgesById.Add(pair.Key, pair.Value);
            }
            context.Settings.maximumOperatingSpeedKmh = graph.maximumOperatingSpeedKmh;

            return TrainAtcPositionLogic.InitializePosition(context, frontPosition, rearPosition);
        }

        public static bool TryCorrectPosition(
            TrainAtcContext context, TrainAtcPosition frontPosition, TrainAtcPosition rearPosition)
        {
            if (context == null)
            {
                return false;
            }
            return TrainAtcPositionLogic.CorrectPosition(context, frontPosition, rearPosition);
        }

        private static bool TryCreateEdgeDictionary(
            TrackAtcGraphDefinition graph, out Dictionary<string, TrackAtcGraphEdge> edgesById)
        {
            edgesById = new();
            if (graph.atcEdge == null || graph.atcEdge.Count == 0 ||
                float.IsNaN(graph.maximumOperatingSpeedKmh) || float.IsInfinity(graph.maximumOperatingSpeedKmh) ||
                graph.maximumOperatingSpeedKmh < 0f)
            {
                return false;
            }

            foreach (var edge in graph.atcEdge)
            {
                if (edge == null || string.IsNullOrEmpty(edge.atcEdgeId) ||
                    !edgesById.TryAdd(edge.atcEdgeId, edge))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
