using System;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Geometry
{
    [Serializable]
    public abstract class TrackGeometryHorizontalSegment
    {
        public float startDistanceM;
        public float lengthM = 100f;

        // 区間起点からの相対位置と従来の近似方位角を評価する。入力はGeometry起点の距離。
        public abstract void EvaluatePosition(float distanceOnGeometryM, out Vector3 position, out float headingDegrees);

        // Geometry距離に対するローカル位置の一次微分を返す。正規化しない。
        public abstract Vector3 EvaluateDerivative(float distanceOnGeometryM);

        // Geometry距離に対するローカル位置の二階微分を返す。端点ではその区間側の値を使う。
        public abstract Vector3 EvaluateSecondDerivative(float distanceOnGeometryM);

        protected float GetLocalDistanceM(float distanceOnGeometryM)
        {
            return distanceOnGeometryM - Mathf.Max(0f, startDistanceM);
        }
    }
}
