using System;

namespace Nakatetsu.Track.Graph.Edge
{
    // 端点の位置と傾きを保持する3次式。旧線形の3次放物線をそのまま表す。
    [Serializable]
    public sealed class TrackEdgeCubicHermiteOffsetSegment : TrackEdgeOffsetSegment
    {
        public float startOffsetM;
        public float endOffsetM;

        // 横オフセットのGeometry距離に対する微分（無次元）。
        public float startSlope;
        public float endSlope;

        public override float EvaluateOffsetM(float distanceOnGeometryM)
        {
            if (!TryParameter(distanceOnGeometryM, out double length, out double t))
                return float.NaN;

            double t2 = t * t, t3 = t2 * t;
            return (float)(startOffsetM + ((double)endOffsetM - startOffsetM) * (3 * t2 - 2 * t3)
                + length * (startSlope * (t3 - 2 * t2 + t) + endSlope * (t3 - t2)));
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            if (!TryParameter(distanceOnGeometryM, out double length, out double t))
                return float.NaN;

            double t2 = t * t;
            return (float)(((double)endOffsetM - startOffsetM) / length * (6 * t - 6 * t2)
                + startSlope * (3 * t2 - 4 * t + 1) + endSlope * (3 * t2 - 2 * t));
        }

        private bool TryParameter(float distance, out double length, out double t)
        {
            length = (double)endDistanceOnGeometryM - startDistanceOnGeometryM;
            t = ((double)distance - startDistanceOnGeometryM) / length;
            return Finite(startDistanceOnGeometryM) && Finite(endDistanceOnGeometryM)
                && Finite(distance) && Finite(startOffsetM) && Finite(endOffsetM)
                && Finite(startSlope) && Finite(endSlope) && length > 0 && t >= 0 && t <= 1;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
