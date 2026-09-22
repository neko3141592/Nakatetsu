using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public abstract class TrackGeometryHorizontalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;

        /// <summary>
        /// Evaluates the position relative to this segment's origin and its existing heading in degrees.
        /// Input is absolute Geometry distance; the caller selects the segment and handles range checks.
        /// Heading retains the existing approximation and is not yet derived from an analytic derivative.
        /// </summary>
        public abstract void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees);

        /// <summary>
        /// Derivative of local position with respect to Geometry distance (m/m), not normalized.
        /// </summary>
        public abstract Vector3 EvaluateDerivative(float distanceOnGeometryM);

        /// <summary>
        /// ローカル位置のGeometry距離に対する二階微分d²P/dS²[1/m]。正規化しない。
        /// 入力はGeometry起点からの距離。区間内で評価し、端点ではその区間側の値を返す。
        /// </summary>
        public abstract Vector3 EvaluateSecondDerivative(float distanceOnGeometryM);

        protected float GetLocalDistanceM(float distanceOnGeometryM)
        {
            return distanceOnGeometryM - Mathf.Max(0f, startDistanceM);
        }
    }
}
