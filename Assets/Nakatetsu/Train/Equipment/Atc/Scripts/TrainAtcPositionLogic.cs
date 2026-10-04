using System;
using System.Collections.Generic;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Atc
{
    internal static class TrainAtcPositionLogic
    {
        internal static bool TryInitializePosition(
            TrainAtcContext context,
            TrackAtcGraphDefinition graph,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.State;
            if (state.isPositionInitialized)
            {
                return false;
            }
            if (graph == null || float.IsNaN(graph.maximumOperatingSpeedKmh) ||
                float.IsInfinity(graph.maximumOperatingSpeedKmh) || graph.maximumOperatingSpeedKmh < 0f)
            {
                return false;
            }
            if (!TryCreateAtcEdgesById(graph, out var atcEdgesById) ||
                !IsValidPosition(atcEdgesById, frontPosition) || !IsValidPosition(atcEdgesById, rearPosition))
            {
                return false;
            }

            // 初期位置と編成の向きは一度だけ設定する。キー操作や運転台変更では初期化し直さない。
            context.Graph = graph;
            context.Settings.maximumOperatingSpeedKmh = graph.maximumOperatingSpeedKmh;
            context.atcEdgesById.Clear();
            foreach (var pair in atcEdgesById)
            {
                context.atcEdgesById.Add(pair.Key, pair.Value);
            }
            state.frontPosition = frontPosition;
            state.rearPosition = rearPosition;
            state.isPositionInitialized = true;
            state.isPositionKnown = true;
            return true;
        }

        internal static bool TryCorrectPosition(
            TrainAtcContext context,
            TrainAtcPosition frontPosition,
            TrainAtcPosition rearPosition)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (!context.State.isPositionInitialized ||
                !IsValidPosition(context.atcEdgesById, frontPosition) || !IsValidPosition(context.atcEdgesById, rearPosition))
            {
                return false;
            }

            // 位置不明からの復帰は、両端の位置を明示的に補正したときだけ行う。
            context.State.frontPosition = frontPosition;
            context.State.rearPosition = rearPosition;
            context.State.isPositionKnown = true;
            return true;
        }

        internal static void UpdateAtcEdgePosition(TrainAtcContext context, TrainAtcBrakePattern retainedPattern)
        {
            var state = context.State;
            var input = context.Input;
            if (!state.isPositionInitialized || !state.isPositionKnown)
            {
                state.isHealthy = false;
                return;
            }
            if (float.IsNaN(input.deltaTimeSeconds) || float.IsInfinity(input.deltaTimeSeconds) ||
                input.deltaTimeSeconds < 0f)
            {
                state.isPositionKnown = false;
                state.isHealthy = false;
                return;
            }
            // 再接続時など、経過時間がない呼び出しでは移動させない。
            if (input.deltaTimeSeconds == 0f) return;
            if (!input.hasSpeedMeasurement || float.IsNaN(input.signedSpeedMps) ||
                float.IsInfinity(input.signedSpeedMps))
            {
                state.isPositionKnown = false;
                state.isHealthy = false;
                return;
            }

            double signedDistanceM = (double)input.signedSpeedMps * input.deltaTimeSeconds;
            var front = state.frontPosition;
            var rear = state.rearPosition;
            List<string> frontPath = null;
            List<string> rearPath = null;
            if (retainedPattern != null)
            {
                if (retainedPattern.isFrontCab)
                {
                    frontPath = retainedPattern.pathAtcEdges;
                }
                else
                {
                    rearPath = retainedPattern.pathAtcEdges;
                }
            }
            if (!TryUpdatePosition(context, input.frontTelegram, signedDistanceM, frontPath, ref front) ||
                !TryUpdatePosition(context, input.rearTelegram, signedDistanceM, rearPath, ref rear))
            {
                // 片側だけ更新しない。最後に確認できた両端の位置は記録として残す。
                state.isPositionKnown = false;
                state.isHealthy = false;
                return;
            }

            state.frontPosition = front;
            state.rearPosition = rear;
        }

        internal static void CaptureCurrentPosition(TrainAtcContext context)
        {
            var state = context.State;
            state.hasCurrentPosition = false;
            state.currentPosition = default;
            state.currentTravelDirection = TrackAtcTravelDirection.Unspecified;
            if (!state.isPositionInitialized || !state.isPositionKnown)
            {
                state.isHealthy = false;
                return;
            }
            if (!state.hasCabState)
            {
                return;
            }

            // 電文と同じ運転台側の位置を選ぶ。TrackSampleはここでは読まない。
            if (state.cab.isFrontCab)
            {
                state.currentPosition = state.frontPosition;
            }
            else
            {
                state.currentPosition = state.rearPosition;
            }
            state.hasCurrentPosition = true;

            if (state.cab.reverserPosition == ReverserPosition.Neutral)
            {
                return;
            }

            // レバーサは有効運転台基準。編成の固定前後に変換してからEdge上の方向を求める。
            bool movesToConsistFront = state.cab.reverserPosition == ReverserPosition.Forward;
            if (!state.cab.isFrontCab)
            {
                movesToConsistFront = !movesToConsistFront;
            }

            state.currentTravelDirection = TrackAtcTravelDirection.BtoA;
            if (state.currentPosition.frontFacesAtoB == movesToConsistFront)
            {
                state.currentTravelDirection = TrackAtcTravelDirection.AtoB;
            }
        }

        private static bool TryCreateAtcEdgesById(
            TrackAtcGraphDefinition graph,
            out Dictionary<string, TrackAtcGraphEdge> atcEdgesById)
        {
            atcEdgesById = null;
            if (graph == null || graph.atcEdge == null)
            {
                return false;
            }

            var edgesById = new Dictionary<string, TrackAtcGraphEdge>();
            foreach (var edge in graph.atcEdge)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.atcEdgeId) ||
                    edgesById.ContainsKey(edge.atcEdgeId))
                {
                    return false;
                }
                edgesById.Add(edge.atcEdgeId, edge);
            }

            atcEdgesById = edgesById;
            return true;
        }

        private static bool IsValidPosition(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            TrainAtcPosition position)
        {
            if (string.IsNullOrWhiteSpace(position.atcEdgeId) ||
                float.IsNaN(position.distanceOnAtcEdgeM) || float.IsInfinity(position.distanceOnAtcEdgeM))
            {
                return false;
            }

            return TryGetAtcEdge(atcEdgesById, position.atcEdgeId, out var edge) &&
                position.distanceOnAtcEdgeM >= 0f && position.distanceOnAtcEdgeM <= edge.lengthM;
        }

        private static bool TryUpdatePosition(
            TrainAtcContext context,
            TrackCircuitAtcTelegram telegram,
            double signedDistanceM,
            List<string> retainedPath,
            ref TrainAtcPosition position)
        {
            if (!IsValidPosition(context.atcEdgesById, position)) return false;
            if (signedDistanceM == 0d) return true;

            var graph = context.Graph;
            bool movesToConsistFront = signedDistanceM > 0d;
            double remainingDistanceM = Math.Abs(signedDistanceM);
            // 無信号中も、前回採用した経路を使って受信機側の分岐を解決する。
            List<string> atcEdgePath = retainedPath;
            // 異常な移動量や循環で無限に探索しない。複数エッジを越える移動は順に処理する。
            for (int i = 0; i <= graph.atcEdge.Count; i++)
            {
                if (!TryGetAtcEdge(context.atcEdgesById, position.atcEdgeId, out var edge)) return false;
                bool movesAtoB = position.frontFacesAtoB == movesToConsistFront;
                double distanceToEndM = position.distanceOnAtcEdgeM;
                if (movesAtoB) distanceToEndM = edge.lengthM - position.distanceOnAtcEdgeM;

                if (remainingDistanceM < distanceToEndM)
                {
                    if (movesAtoB)
                    {
                        position.distanceOnAtcEdgeM += (float)remainingDistanceM;
                    }
                    else
                    {
                        position.distanceOnAtcEdgeM -= (float)remainingDistanceM;
                    }
                    return true;
                }

                string exitNodeId = edge.atcNodeAId;
                var direction = TrackAtcTravelDirection.BtoA;
                if (movesAtoB)
                {
                    exitNodeId = edge.atcNodeBId;
                    direction = TrackAtcTravelDirection.AtoB;
                }
                if (!TryResolveNextAtcEdge(context, telegram, edge.atcEdgeId, direction,
                    exitNodeId, ref atcEdgePath, out var nextEdge))
                {
                    // ちょうど終端に到達した場合は、終端位置までは特定できている。
                    if (remainingDistanceM == distanceToEndM)
                    {
                        position.distanceOnAtcEdgeM = 0f;
                        if (movesAtoB) position.distanceOnAtcEdgeM = edge.lengthM;
                        return true;
                    }
                    return false;
                }

                remainingDistanceM -= distanceToEndM;
                bool entersAtNodeA = nextEdge.atcNodeAId == exitNodeId;
                if (entersAtNodeA && nextEdge.atcNodeBId == exitNodeId) return false;
                position.atcEdgeId = nextEdge.atcEdgeId;
                position.distanceOnAtcEdgeM = nextEdge.lengthM;
                if (entersAtNodeA) position.distanceOnAtcEdgeM = 0f;
                position.frontFacesAtoB = entersAtNodeA == movesToConsistFront;
                if (remainingDistanceM == 0d) return true;
            }
            return false;
        }

        private static bool TryGetAtcEdge(
            IReadOnlyDictionary<string, TrackAtcGraphEdge> atcEdgesById,
            string atcEdgeId, out TrackAtcGraphEdge edge)
        {
            edge = null;
            if (atcEdgesById == null || string.IsNullOrWhiteSpace(atcEdgeId) ||
                !atcEdgesById.TryGetValue(atcEdgeId, out edge))
            {
                return false;
            }
            return edge != null && edge.atcEdgeId == atcEdgeId &&
                !float.IsNaN(edge.lengthM) && !float.IsInfinity(edge.lengthM) && edge.lengthM > 0f;
        }

        private static bool TryResolveNextAtcEdge(
            TrainAtcContext context,
            TrackCircuitAtcTelegram telegram,
            string atcEdgeId,
            TrackAtcTravelDirection direction,
            string exitNodeId,
            ref List<string> atcEdgePath,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            if (string.IsNullOrWhiteSpace(exitNodeId)) return false;
            if (telegram != null && telegram.isValid &&
                telegram.atcRouteInfomation.TryGetValue((atcEdgeId, direction), out var route) && route != null &&
                TryResolveNextOnPath(context, route.atcEdgePath, atcEdgeId, exitNodeId, out nextEdge))
            {
                atcEdgePath = route.atcEdgePath;
                return true;
            }
            // 同じ更新で複数エッジを越える場合も、読み取った電文の経路を引き継ぐ。
            if (TryResolveNextOnPath(context, atcEdgePath, atcEdgeId, exitNodeId, out nextEdge)) return true;
            return TryResolveNextOnGraph(context, atcEdgeId, exitNodeId, out nextEdge);
        }

        private static bool TryResolveNextOnPath(
            TrainAtcContext context,
            List<string> atcEdgePath,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            if (atcEdgePath == null) return false;
            int currentIndex = -1;
            for (int i = 0; i < atcEdgePath.Count; i++)
            {
                if (atcEdgePath[i] != atcEdgeId) continue;
                if (currentIndex >= 0) return false;
                currentIndex = i;
            }
            if (currentIndex < 0) return false;

            // 電文の進行順と逆方向に移動する場合は、手前のエッジも候補にする。
            for (int i = currentIndex - 1; i <= currentIndex + 1; i += 2)
            {
                if (i < 0 || i >= atcEdgePath.Count) continue;
                if (!TryGetAtcEdge(context.atcEdgesById, atcEdgePath[i], out var candidate)) return false;
                if (candidate.atcNodeAId != exitNodeId && candidate.atcNodeBId != exitNodeId) continue;
                if (nextEdge != null) return false;
                nextEdge = candidate;
            }
            return nextEdge != null;
        }

        private static bool TryResolveNextOnGraph(
            TrainAtcContext context,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            int candidateCount = 0;
            foreach (var edge in context.atcEdgesById.Values)
            {
                if (edge == null || edge.atcEdgeId == atcEdgeId) continue;
                if (edge.atcNodeAId != exitNodeId && edge.atcNodeBId != exitNodeId) continue;
                candidateCount++;
                nextEdge = edge;
            }
            if (candidateCount == 1) return TryGetAtcEdge(context.atcEdgesById, nextEdge.atcEdgeId, out nextEdge);
            if (candidateCount == 0) return false;
            return TryResolveNextOnRoute(context, atcEdgeId, exitNodeId, out nextEdge);
        }

        private static bool TryResolveNextOnRoute(
            TrainAtcContext context,
            string atcEdgeId,
            string exitNodeId,
            out TrackAtcGraphEdge nextEdge)
        {
            nextEdge = null;
            var graph = context.Graph;
            if (graph.routes == null) return false;
            // 開通状態によらず、定義済みの進路から分岐先が一つに絞れる場合は解決する。
            foreach (var route in graph.routes)
            {
                if (route == null || !TryResolveNextOnPath(context, route.atcEdgeIds,
                    atcEdgeId, exitNodeId, out var candidate)) continue;
                if (nextEdge != null && nextEdge.atcEdgeId != candidate.atcEdgeId) return false;
                nextEdge = candidate;
            }
            return nextEdge != null;
        }
    }
}
