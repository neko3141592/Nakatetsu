using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.Atc
{
    internal static class TrackAtcOutputLogic
    {
        internal static void UpdateOutput(TrackAtcContext context)
        {
            context.Output.telegrams.Clear();
            foreach (var pair in context.State.telegram.circuitsById)
            {
                var telegram = CreateTelegram(context, pair.Value);
                context.Output.telegrams.Add(pair.Key, telegram);
            }
        }

        private static TrackCircuitAtcTelegram CreateTelegram(
            TrackAtcContext context,
            TrackAtcTelegramCircuitResult circuit)
        {
            var telegram = new TrackCircuitAtcTelegram
            {
                issuedAtSeconds = context.Input.simulationTimeSeconds,
                isValid = circuit.isValid
            };
            if (!circuit.isValid)
            {
                return telegram;
            }

            foreach (var routeKey in circuit.routeKeys)
            {
                if (!TryConvertDirection(routeKey.direction, out var direction) ||
                    !TryCreateRouteInformation(context, routeKey, out var routeInformation))
                {
                    // 通常の有効性は工程5で確定している。工程間の不整合で変換
                    // できない場合もStateは変更せず、部分的な有効電文を外へ出さない。
                    telegram.isValid = false;
                    telegram.routeAtoB = null;
                    telegram.routeBtoA = null;
                    return telegram;
                }

                if (direction == TrackAtcTravelDirection.AtoB)
                {
                    telegram.routeAtoB = routeInformation;
                }
                else
                {
                    telegram.routeBtoA = routeInformation;
                }
            }

            return telegram;
        }

        private static bool TryCreateRouteInformation(
            TrackAtcContext context,
            TrackAtcEdgeKey routeKey,
            out TrackCircuitAtcRouteInfomation routeInformation)
        {
            routeInformation = null;
            var results = context.State.path.resultsByKey;
            if (!results.TryGetValue(routeKey, out var root) || root == null ||
                !context.State.protectionMode.resultsByKey.TryGetValue(routeKey, out var protection) ||
                protection == null)
            {
                return false;
            }

            var stopKey = new TrackAtcEdgeKey(root.stopAtcEdgeId, root.stopTravelDirection);
            var atcEdgePath = new List<string>();
            var visited = new HashSet<TrackAtcEdgeKey>();
            var currentKey = routeKey;
            while (visited.Add(currentKey))
            {
                if (!results.TryGetValue(currentKey, out var current) || current == null)
                {
                    return false;
                }

                atcEdgePath.Add(currentKey.atcEdgeId);
                if (currentKey == stopKey)
                {
                    // 後続の起点結果がさらに先へ進めても、最初の起点に対する
                    // 停止限界で必ず打ち切り、途中の停止限界へ置き換えない。
                    routeInformation = new TrackCircuitAtcRouteInfomation
                    {
                        atcEdgePath = atcEdgePath,
                        stopAtcEdgeId = root.stopAtcEdgeId,
                        overrunProtectionMode = protection.overrunProtectionMode
                    };
                    return true;
                }

                if (!current.nextEdgeKey.HasValue)
                {
                    return false;
                }

                currentKey = current.nextEdgeKey.Value;
            }

            return false;
        }

        private static bool TryConvertDirection(
            TrackEdgeTravelDirection direction,
            out TrackAtcTravelDirection telegramDirection)
        {
            switch (direction)
            {
                case TrackEdgeTravelDirection.AtoB:
                    telegramDirection = TrackAtcTravelDirection.AtoB;
                    return true;
                case TrackEdgeTravelDirection.BtoA:
                    telegramDirection = TrackAtcTravelDirection.BtoA;
                    return true;
                default:
                    telegramDirection = TrackAtcTravelDirection.Unspecified;
                    return false;
            }
        }
    }
}
