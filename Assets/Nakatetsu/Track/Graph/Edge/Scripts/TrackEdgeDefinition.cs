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

        // Shape-specific settings are saved in Geometry coordinates, independently of Edge orientation.
        [SerializeReference] public List<TrackEdgeOffsetSegment> offsetSegments = new();

        // Generated samples ordered from Node A to Node B. Do not modify during simulation.
        [HideInInspector] public List<TrackEdgeDistanceSample> distanceMap = new();

        /// <summary>
        /// Geometry距離[m]に対応するOffsetSegmentから横オフセット[m]を取得する。LUTは使用しない。
        /// offsetSegmentsはGeometry開始距離の昇順、各区間はstart &lt; end、重なりなしで保持する。
        /// 区間の両端は評価可能。隣接区間の共有端点では開始距離が大きい側を採用する。
        /// Edge範囲外・区間なし・不正な定義・非有限の評価値はfalse。定義やリストは変更しない。
        /// </summary>
        public bool TryEvaluateOffsetAtGeometryDistance(float distanceOnGeometryM, out float offsetM)
        {
            return TryEvaluateOffsetAtGeometryDistance(distanceOnGeometryM, out offsetM, out _);
        }

        /// <summary>
        /// 同じ区間からオフセット[m]とGeometry距離に対する微分do/dS[m/m]を取得する。
        /// どちらかが非有限ならfalseを返し、両出力を0にする。Edgeの向きで微分の符号は変えない。
        /// </summary>
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
                return false;

            TrackEdgeOffsetSegment selected = null;
            float previousEndM = 0f;
            for (int i = 0; i < offsetSegments.Count; i++)
            {
                var segment = offsetSegments[i];
                if (segment == null || !IsFinite(segment.startDistanceOnGeometryM)
                    || !IsFinite(segment.endDistanceOnGeometryM) || segment.startDistanceOnGeometryM < 0f
                    || segment.startDistanceOnGeometryM >= segment.endDistanceOnGeometryM
                    || (i > 0 && segment.startDistanceOnGeometryM < previousEndM))
                    return false;

                if (distanceOnGeometryM >= segment.startDistanceOnGeometryM
                    && distanceOnGeometryM <= segment.endDistanceOnGeometryM)
                    selected = segment;

                previousEndM = segment.endDistanceOnGeometryM;
            }

            if (selected == null) return false;
            float value = selected.EvaluateOffsetM(distanceOnGeometryM);
            float derivative = selected.EvaluateDerivative(distanceOnGeometryM);
            if (!IsFinite(value) || !IsFinite(derivative)) return false;
            offsetM = value;
            offsetDerivative = derivative;
            return true;
        }

        /// <summary>
        /// Node AからのEdge実距離[m]を、LUTの二分探索と線形補間でGeometry距離[m]へ変換する。
        /// LUTは2点以上、全値が有限、Edge距離が0から厳密な昇順であることを生成・読込時に保証する。
        /// Geometry距離は逆向きEdgeでは降順でもよい。全件検査はせず、O(log n)で読み取る。
        /// 点数不足・範囲外・探索中に検出した不正値はfalse。ClampやLUTの変更は行わない。
        /// </summary>
        public bool TryConvertToGeometryDistance(float distanceOnEdgeM, out float distanceOnGeometryM)
        {
            distanceOnGeometryM = default;
            if (!IsFinite(distanceOnEdgeM) || distanceMap == null || distanceMap.Count < 2) return false;

            int lowerIndex = 0;
            int upperIndex = distanceMap.Count - 1;
            var lower = distanceMap[lowerIndex];
            var upper = distanceMap[upperIndex];
            if (!IsFinite(lower) || !IsFinite(upper) || lower.distanceOnEdgeM != 0f
                || upper.distanceOnEdgeM <= lower.distanceOnEdgeM
                || distanceOnEdgeM < lower.distanceOnEdgeM || distanceOnEdgeM > upper.distanceOnEdgeM)
                return false;

            while (upperIndex - lowerIndex > 1)
            {
                int middleIndex = lowerIndex + (upperIndex - lowerIndex) / 2;
                var middle = distanceMap[middleIndex];
                if (!IsFinite(middle) || middle.distanceOnEdgeM <= lower.distanceOnEdgeM
                    || middle.distanceOnEdgeM >= upper.distanceOnEdgeM)
                    return false;

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

            // Preserve stored values exactly at sample points, including both endpoints.
            if (distanceOnEdgeM == lower.distanceOnEdgeM)
                distanceOnGeometryM = lower.distanceOnGeometryM;
            else if (distanceOnEdgeM == upper.distanceOnEdgeM)
                distanceOnGeometryM = upper.distanceOnGeometryM;
            else
            {
                // Double intermediates avoid overflow when subtracting finite float values.
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
