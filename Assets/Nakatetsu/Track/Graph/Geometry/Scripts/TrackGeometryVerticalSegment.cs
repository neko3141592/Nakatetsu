using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public abstract class TrackGeometryVerticalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;

        protected const float MinSegmentLengthM = 0.001f;
        protected const float PermilleScale = 1000f;

        /// <summary>Height relative to this segment's start, in metres. Input is absolute Geometry distance.</summary>
        public abstract float EvaluateHeightDeltaM(float distanceOnGeometryM);

        /// <summary>
        /// dy/dS in m/m, not permille, evaluated within the selected segment (including endpoint slopes).
        /// The caller selects the segment and handles gaps. Degenerate segments return zero and are skipped.
        /// </summary>
        public abstract float EvaluateDerivative(float distanceOnGeometryM);

        /// <summary>
        /// 高さのGeometry距離に対する二階微分d²y/dS²[1/m]。入力はGeometry起点からの距離。
        /// 区間内で評価し、端点ではその区間側の値を返す。縮退区間は0を返す。
        /// </summary>
        public abstract float EvaluateSecondDerivative(float distanceOnGeometryM);

        protected float GetLocalDistanceM(float distanceOnGeometryM)
        {
            return distanceOnGeometryM - Mathf.Max(0f, startDistanceM);
        }
    }
}
