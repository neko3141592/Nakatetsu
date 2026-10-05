using System;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Simulation.Atc
{
    public static class TrainAtcReceiverLogic
    {
        public static void Calculate(TrainAtcReceiverContext context,
            TrackGraphContext graph = null, TrackCircuitSimulationState circuits = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var input = context.Input;
            var output = context.Output;

            // 位置を取得できない場合は前ステップの結果を残さない。
            output.hasEdgePosition = false;
            output.edgeId = null;
            output.distanceOnEdgeM = 0f;
            output.telegram = null;
            if (!input.hasEdgePosition || string.IsNullOrWhiteSpace(input.edgeId)
                || float.IsNaN(input.distanceOnEdgeM) || float.IsInfinity(input.distanceOnEdgeM)
                || input.distanceOnEdgeM < 0f)
            {
                return;
            }

            output.hasEdgePosition = true;
            output.edgeId = input.edgeId;
            output.distanceOnEdgeM = input.distanceOnEdgeM;

            if (circuits != null && TryResolveCircuitId(graph, input.edgeId,
                input.distanceOnEdgeM, out string circuitId)
                && circuits.TryGetAtcTelegram(circuitId, out var telegram))
            {
                // 不正電文も受信結果として保持し、有効性の判断は車上ATCに任せる。
                output.telegram = telegram.Clone();
            }
        }

        public static bool TryResolveCircuitId(TrackGraphContext graph, string edgeId,
            float distanceOnEdgeM, out string circuitId)
        {
            circuitId = null;
            if (graph == null || !graph.IsInitialized || !graph.TryGetEdge(edgeId, out var edge)
                || float.IsNaN(distanceOnEdgeM) || float.IsInfinity(distanceOnEdgeM)
                || distanceOnEdgeM < 0f || distanceOnEdgeM > edge.LengthM)
            {
                return false;
            }

            string selectedId = null;
            foreach (var circuit in graph.Circuits)
            {
                if (circuit.sections == null) return false;
                foreach (var section in circuit.sections)
                {
                    if (section == null) return false;
                    if (section.edgeId != edgeId) continue;
                    if (!edge.TryConvertToEdgeDistance(section.startDistanceOnGeometryM, out float start)
                        || !edge.TryConvertToEdgeDistance(section.endDistanceOnGeometryM, out float end)
                        || start == end)
                    {
                        return false;
                    }

                    float lower = Math.Min(start, end);
                    float upper = Math.Max(start, end);
                    // 共有境界はAからの距離が大きい側の区間。Edge終端だけは区間に含める。
                    if (distanceOnEdgeM < lower || distanceOnEdgeM > upper
                        || distanceOnEdgeM == upper && upper != edge.LengthM)
                    {
                        continue;
                    }

                    if (selectedId != null && selectedId != circuit.circuitId) return false;
                    selectedId = circuit.circuitId;
                }
            }

            circuitId = selectedId;
            return circuitId != null;
        }

        public static bool TryGetTelegram(TrainAtcReceiverContext context, out TrackCircuitAtcTelegram telegram)
        {
            telegram = null;
            if (context == null || context.Output.telegram == null) return false;
            telegram = context.Output.telegram.Clone();
            return true;
        }

        public static bool TryGetEdgePosition(TrainAtcReceiverContext context,
            out string edgeId, out float distanceOnEdgeM)
        {
            edgeId = null;
            distanceOnEdgeM = 0f;
            if (context == null || !context.Output.hasEdgePosition)
            {
                return false;
            }

            edgeId = context.Output.edgeId;
            distanceOnEdgeM = context.Output.distanceOnEdgeM;
            return true;
        }
    }
}
