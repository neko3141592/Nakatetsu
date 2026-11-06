using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    public static class BackgroundVertexCalculator
    {
        public static bool TryEvaluatePoint(TrackGraphContext graph, IReadOnlyList<GuideLineDefinition> guideLines,
            BackgroundVertexDefinition vertex, out Vector3 point)
        {
            point = default;
            return graph != null
                && vertex != null
                && GuideLineCalculator.TryFindDefinition(guideLines, vertex.GuideLineId, out var guideLine)
                && graph.TryGetEdge(guideLine.EdgeId, out var edge)
                && edge.TryConvertToEdgeDistance(vertex.DistanceOnGeometryM, out float distanceOnEdgeM)
                && GuideLineCalculator.TryEvaluatePoint(graph, guideLine, distanceOnEdgeM, out point);
        }
    }
}
