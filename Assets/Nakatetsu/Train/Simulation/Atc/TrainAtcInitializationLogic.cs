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
            out TrainAtcPosition position,
            string selectedAtcEdgeId = null)
        {
            position = null;
            if (graph == null || graph.atcEdge == null || string.IsNullOrWhiteSpace(trackEdgeId) ||
                float.IsNaN(distanceOnEdgeM) || float.IsInfinity(distanceOnEdgeM) || distanceOnEdgeM < 0f)
            {
                return false;
            }

            TrainAtcPosition matchedPosition = null;
            TrainAtcPosition endPosition = null;
            bool hasMultipleMatches = false;
            bool hasMultipleEndMatches = false;
            foreach (var edge in graph.atcEdge)
            {
                if (!TryMapPosition(edge, trackEdgeId, distanceOnEdgeM, frontFacesAtoB,
                    out var candidate, out bool isUpperBoundary))
                {
                    continue;
                }

                // 共通区間の定位・反位は、受信した選択済みEdgeで一意に決める。
                if (edge.atcEdgeId == selectedAtcEdgeId)
                {
                    position = candidate;
                    return true;
                }
                if (isUpperBoundary)
                {
                    hasMultipleEndMatches |= endPosition != null;
                    endPosition = candidate;
                }
                else
                {
                    hasMultipleMatches |= matchedPosition != null;
                    matchedPosition = candidate;
                }
            }

            // 物理区間の共有境界は距離の大きい側。続きがない終端だけ終点を採用する。
            if (hasMultipleMatches || matchedPosition == null && hasMultipleEndMatches)
            {
                return false;
            }
            position = matchedPosition ?? endPosition;
            return position != null;
        }

        private static bool TryMapPosition(
            TrackAtcGraphEdge edge, string trackEdgeId, float distanceOnEdgeM, bool frontFacesAtoB,
            out TrainAtcPosition position, out bool isUpperBoundary)
        {
            position = null;
            isUpperBoundary = false;
            if (edge == null || string.IsNullOrEmpty(edge.atcEdgeId) || edge.physicalSpans == null ||
                float.IsNaN(edge.lengthM) || float.IsInfinity(edge.lengthM) || edge.lengthM <= 0f)
            {
                return false;
            }

            float startOnAtcEdgeM = 0f;
            foreach (var span in edge.physicalSpans)
            {
                if (span == null || string.IsNullOrEmpty(span.trackEdgeId) ||
                    float.IsNaN(span.startDistanceOnEdgeM) || float.IsInfinity(span.startDistanceOnEdgeM) ||
                    float.IsNaN(span.endDistanceOnEdgeM) || float.IsInfinity(span.endDistanceOnEdgeM) ||
                    span.startDistanceOnEdgeM < 0f || span.endDistanceOnEdgeM < 0f ||
                    span.startDistanceOnEdgeM == span.endDistanceOnEdgeM)
                {
                    return false;
                }

                float lowerM = Math.Min(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                float upperM = Math.Max(span.startDistanceOnEdgeM, span.endDistanceOnEdgeM);
                if (span.trackEdgeId == trackEdgeId && distanceOnEdgeM >= lowerM && distanceOnEdgeM <= upperM)
                {
                    if (position != null)
                    {
                        return false;
                    }
                    position = new TrainAtcPosition
                    {
                        atcEdgeId = edge.atcEdgeId,
                        distanceOnAtcEdgeM = startOnAtcEdgeM + Math.Abs(distanceOnEdgeM - span.startDistanceOnEdgeM),
                        frontFacesAtoB = frontFacesAtoB == (span.endDistanceOnEdgeM > span.startDistanceOnEdgeM)
                    };
                    isUpperBoundary = distanceOnEdgeM == upperM;
                }
                startOnAtcEdgeM += upperM - lowerM;
            }

            // 結合区間の実距離を使う。勾配・速度プロファイルと同じATC座標にする。
            if (position == null || Math.Abs(startOnAtcEdgeM - edge.lengthM) > 0.001f ||
                position.distanceOnAtcEdgeM > edge.lengthM + 0.001f)
            {
                return false;
            }
            position.distanceOnAtcEdgeM = Math.Min(position.distanceOnAtcEdgeM, edge.lengthM);
            return true;
        }
    }
}
