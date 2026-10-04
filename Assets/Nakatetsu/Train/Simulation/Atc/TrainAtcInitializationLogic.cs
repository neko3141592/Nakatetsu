using System;
using Nakatetsu.Track.Atc;
using Nakatetsu.Train.Equipment.Atc;

namespace Nakatetsu.Train.Simulation.Atc
{
    public static class TrainAtcInitializationLogic
    {
        public static bool TryResolvePosition(
            TrackAtcGraphDefinition graph,
            string trackEdgeId,
            float distanceOnEdgeM,
            bool frontFacesAtoB,
            out TrainAtcPosition position)
        {
            position = default;
            if (graph == null || graph.atcEdge == null || string.IsNullOrWhiteSpace(trackEdgeId) ||
                float.IsNaN(distanceOnEdgeM) || float.IsInfinity(distanceOnEdgeM))
            {
                return false;
            }

            TrackAtcGraphEdge matchedEdge = null;
            TrackAtcGraphEdge endEdge = null;
            bool hasMultipleEndEdges = false;
            foreach (var edge in graph.atcEdge)
            {
                if (edge == null || edge.trackEdgeId != trackEdgeId) continue;
                if (string.IsNullOrWhiteSpace(edge.atcEdgeId) ||
                    float.IsNaN(edge.startDistanceOnEdgeM) || float.IsInfinity(edge.startDistanceOnEdgeM) ||
                    float.IsNaN(edge.endDistanceOnEdgeM) || float.IsInfinity(edge.endDistanceOnEdgeM) ||
                    edge.startDistanceOnEdgeM < 0f || edge.endDistanceOnEdgeM <= edge.startDistanceOnEdgeM)
                {
                    return false;
                }
                if (distanceOnEdgeM < edge.startDistanceOnEdgeM || distanceOnEdgeM > edge.endDistanceOnEdgeM)
                {
                    continue;
                }

                // 境界は始点側の区間に含める。続きがない終端だけ終点側の区間を使う。
                if (distanceOnEdgeM == edge.endDistanceOnEdgeM)
                {
                    if (endEdge != null) hasMultipleEndEdges = true;
                    endEdge = edge;
                    continue;
                }
                if (matchedEdge != null) return false;
                matchedEdge = edge;
            }

            if (matchedEdge == null)
            {
                if (hasMultipleEndEdges) return false;
                matchedEdge = endEdge;
            }
            if (matchedEdge == null || float.IsNaN(matchedEdge.lengthM) ||
                float.IsInfinity(matchedEdge.lengthM) || matchedEdge.lengthM <= 0f)
            {
                return false;
            }

            float distanceOnAtcEdgeM = distanceOnEdgeM - matchedEdge.startDistanceOnEdgeM;
            if (distanceOnAtcEdgeM > matchedEdge.lengthM + 0.001f) return false;
            position.atcEdgeId = matchedEdge.atcEdgeId;
            position.distanceOnAtcEdgeM = Math.Min(distanceOnAtcEdgeM, matchedEdge.lengthM);
            // コンパイル済みATC EdgeのA→Bは、物理Edgeの距離が増える方向と一致する。
            position.frontFacesAtoB = frontFacesAtoB;
            return true;
        }
    }
}
