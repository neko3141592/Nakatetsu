using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using UnityEngine;

namespace Nakatetsu.Track.Debugging
{
    public readonly struct TrackEdgeDebugLabel
    {
        public readonly float DistanceM;
        public readonly Vector3 Position;
        public readonly string Text;

        public TrackEdgeDebugLabel(string edgeId, float distanceM, Vector3 position)
        {
            DistanceM = distanceM;
            Position = position;
            Text = $"{edgeId}  {distanceM:0} m";
        }
    }

    public sealed class TrackEdgeDebugSamples
    {
        public string EdgeId { get; }
        public Vector3[] LinePositions { get; }
        public TrackEdgeDebugLabel[] Labels { get; }

        public TrackEdgeDebugSamples(string edgeId, Vector3[] linePositions, TrackEdgeDebugLabel[] labels)
        {
            EdgeId = edgeId;
            LinePositions = linePositions;
            Labels = labels;
        }
    }

    public static class TrackEdgeDebugSampling
    {
        public const float LabelIntervalM = 50f;
        private const float EndpointToleranceM = 0.001f;
        private const int MaximumLineSegments = 20000;
        private const int MaximumLabels = 20000;

        // 距離表に基づくEdge実距離を使う。Geometry距離やNode間の直線距離ではない。
        public static bool TrySample(TrackGraphContext graph, TrackEdgeDefinition edge,
            float lineIntervalM, out TrackEdgeDebugSamples samples)
        {
            samples = null;
            if (edge == null || !IsFinite(edge.LengthM) || edge.LengthM <= 0f)
                return false;

            float interval = IsFinite(lineIntervalM) ? Mathf.Max(0.1f, lineIntervalM) : 5f;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(MaximumLineSegments, edge.LengthM / interval)));
            // ラベルを途中で打ち切って正常表示と誤認させない。
            if ((edge.LengthM + EndpointToleranceM) / LabelIntervalM >= MaximumLabels) return false;
            var positions = new Vector3[segmentCount + 1];
            for (int i = 0; i <= segmentCount; i++)
            {
                float distance = edge.LengthM * ((float)i / segmentCount);
                if (!TrackEdgeCalculator.TryEvaluate(graph, edge.edgeId, distance, out var sample))
                    return false;
                positions[i] = sample.Position;
            }

            // LUTの積算誤差で端点が49.99999m等になっても、50m標識を落とさない。
            int labelCount = Mathf.FloorToInt((edge.LengthM + EndpointToleranceM) / LabelIntervalM) + 1;
            var labels = new List<TrackEdgeDebugLabel>(labelCount);
            for (int i = 0; i < labelCount; i++)
            {
                float markerDistance = i * LabelIntervalM;
                float sampleDistance = Mathf.Min(markerDistance, edge.LengthM);
                if (!TrackEdgeCalculator.TryEvaluate(graph, edge.edgeId, sampleDistance, out var sample))
                    return false;
                labels.Add(new TrackEdgeDebugLabel(edge.edgeId, markerDistance, sample.Position));
            }

            samples = new TrackEdgeDebugSamples(edge.edgeId, positions, labels.ToArray());
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
