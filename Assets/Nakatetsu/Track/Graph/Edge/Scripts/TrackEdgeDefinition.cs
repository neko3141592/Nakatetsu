using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.Graph.Edge
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph", sourceAssembly: "Nakatetsu.Track.Graph", sourceClassName: "TrackEdgeDefinition")]
    public sealed class TrackEdgeDefinition
    {
        public string edgeId;

        public string nodeAId;
        public string nodeBId;

        [FormerlySerializedAs("guideLineId")]
        [FormerlySerializedAs("trainGeometryId")]
        [FormerlySerializedAs("trackGeometryId")]
        public string geometryId;

        [FormerlySerializedAs("startDistanceOnGuideLineM")]
        [FormerlySerializedAs("startDistanceOnTrainGeometryM")]
        [FormerlySerializedAs("startDistanceOnTrackGeometryM")]
        public float startDistanceOnGeometryM;
        [FormerlySerializedAs("endDistanceOnGuideLineM")]
        [FormerlySerializedAs("endDistanceOnTrainGeometryM")]
        [FormerlySerializedAs("endDistanceOnTrackGeometryM")]
        public float endDistanceOnGeometryM;

        [SerializeReference] public List<TrackEdgeOffsetSegment> offsetSegments = new();

        [HideInInspector] public List<TrackEdgeDistanceSample> distanceMap = new();

        public float LengthM => distanceMap != null && distanceMap.Count >= 2
            ? distanceMap[^1].distanceOnEdgeM
            : 0f;

        // Geometry距離に対応する区間から横オフセットを取得する。共有端点では後の区間を採用する。
        public bool TryEvaluateOffsetAtGeometryDistance(float distanceOnGeometryM, out float offsetM)
        {
            return TryEvaluateOffsetAtGeometryDistance(distanceOnGeometryM, out offsetM, out _);
        }

        // 横オフセットとGeometry距離に対する微分を取得する。Edgeの向きで微分の符号は変えない。
        public bool TryEvaluateOffsetAtGeometryDistance(float distanceOnGeometryM, out float offsetM,
            out float offsetDerivative)
        {
            offsetM = default;
            offsetDerivative = default;
            if (!IsFinite(distanceOnGeometryM) || !IsFinite(startDistanceOnGeometryM)
                || !IsFinite(endDistanceOnGeometryM) || startDistanceOnGeometryM < 0f
                || endDistanceOnGeometryM < 0f || startDistanceOnGeometryM == endDistanceOnGeometryM
                || distanceOnGeometryM < Mathf.Min(startDistanceOnGeometryM, endDistanceOnGeometryM)
                || distanceOnGeometryM > Mathf.Max(startDistanceOnGeometryM, endDistanceOnGeometryM)
                || offsetSegments == null || offsetSegments.Count == 0)
            {
                return false;
            }

            TrackEdgeOffsetSegment selected = null;
            float previousEndM = 0f;

            for (int i = 0; i < offsetSegments.Count; i++)
            {
                var segment = offsetSegments[i];
                if (segment == null || !IsFinite(segment.startDistanceOnGeometryM)
                    || !IsFinite(segment.endDistanceOnGeometryM) || segment.startDistanceOnGeometryM < 0f
                    || segment.startDistanceOnGeometryM >= segment.endDistanceOnGeometryM
                    || (i > 0 && segment.startDistanceOnGeometryM < previousEndM))
                {
                    return false;
                }

                if (distanceOnGeometryM >= segment.startDistanceOnGeometryM
                    && distanceOnGeometryM <= segment.endDistanceOnGeometryM)
                {
                    selected = segment;
                }

                previousEndM = segment.endDistanceOnGeometryM;
            }

            if (selected == null)
            {
                return false;
            }

            float value = selected.EvaluateOffsetM(distanceOnGeometryM);
            float derivative = selected.EvaluateDerivative(distanceOnGeometryM);
            if (!IsFinite(value) || !IsFinite(derivative))
            {
                return false;
            }

            offsetM = value;
            offsetDerivative = derivative;
            return true;
        }

        // Node A起点のEdge実距離を距離表で二分探索し、Geometry距離へ変換する。
        public bool TryConvertToGeometryDistance(float distanceOnEdgeM, out float distanceOnGeometryM)
        {
            distanceOnGeometryM = default;
            if (!IsFinite(distanceOnEdgeM) || distanceMap == null || distanceMap.Count < 2)
            {
                return false;
            }

            int lowerIndex = 0;
            int upperIndex = distanceMap.Count - 1;
            var lower = distanceMap[lowerIndex];
            var upper = distanceMap[upperIndex];
            if (!IsFinite(lower) || !IsFinite(upper) || lower.distanceOnEdgeM != 0f
                || upper.distanceOnEdgeM <= lower.distanceOnEdgeM
                || distanceOnEdgeM < lower.distanceOnEdgeM || distanceOnEdgeM > upper.distanceOnEdgeM)
            {
                return false;
            }

            while (upperIndex - lowerIndex > 1)
            {
                int middleIndex = lowerIndex + (upperIndex - lowerIndex) / 2;
                var middle = distanceMap[middleIndex];
                if (!IsFinite(middle) || middle.distanceOnEdgeM <= lower.distanceOnEdgeM
                    || middle.distanceOnEdgeM >= upper.distanceOnEdgeM)
                {
                    return false;
                }

                if (distanceOnEdgeM < middle.distanceOnEdgeM)
                {
                    upperIndex = middleIndex;
                    upper = middle;
                }
                else
                {
                    lowerIndex = middleIndex;
                    lower = middle;
                }
            }

            // 距離表の点と一致する場合は保存値をそのまま返す。
            if (distanceOnEdgeM == lower.distanceOnEdgeM)
            {
                distanceOnGeometryM = lower.distanceOnGeometryM;
            }
            else if (distanceOnEdgeM == upper.distanceOnEdgeM)
            {
                distanceOnGeometryM = upper.distanceOnGeometryM;
            }
            else
            {
                // 差分の計算にはdoubleを使い、floatの桁あふれを避ける。
                double t = ((double)distanceOnEdgeM - lower.distanceOnEdgeM) / ((double)upper.distanceOnEdgeM - lower.distanceOnEdgeM);
                distanceOnGeometryM = (float)(lower.distanceOnGeometryM
                    + ((double)upper.distanceOnGeometryM - lower.distanceOnGeometryM) * t);
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(TrackEdgeDistanceSample sample) =>
            IsFinite(sample.distanceOnEdgeM) && IsFinite(sample.distanceOnGeometryM);
    }

    [Serializable]
    public struct TrackEdgeDistanceSample
    {
        public float distanceOnGeometryM;
        public float distanceOnEdgeM;
    }
}
