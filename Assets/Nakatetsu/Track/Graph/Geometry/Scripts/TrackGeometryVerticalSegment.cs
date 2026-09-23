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

        // Geometry距離から区間起点に対する高さ[m]を求める。
        public abstract float EvaluateHeightDeltaM(float distanceOnGeometryM);

        // Geometry距離に対する高さの一次微分[m/m]を返す。縮退区間は0とする。
        public abstract float EvaluateDerivative(float distanceOnGeometryM);

        // Geometry距離に対する高さの二階微分を返す。端点ではその区間側の値を使う。
        public abstract float EvaluateSecondDerivative(float distanceOnGeometryM);

        protected float GetLocalDistanceM(float distanceOnGeometryM)
        {
            return distanceOnGeometryM - Mathf.Max(0f, startDistanceM);
        }
    }
}
