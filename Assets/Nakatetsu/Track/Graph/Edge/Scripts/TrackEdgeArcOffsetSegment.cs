using System;

namespace Nakatetsu.Track.Graph.Edge
{
    // Geometry距離を基準にした円弧状の横オフセット。
    [Serializable]
    public sealed class TrackEdgeArcOffsetSegment : TrackEdgeOffsetSegment
    {
        private const double MinimumRadiusM = 0.001;
        private const double DomainTolerance = 0.00001;

        public float startOffsetM;
        public float radiusM;
        public float startHeadingDeg;

        public override float EvaluateOffsetM(float distanceOnGeometryM)
        {
            if (!TryEvaluate(distanceOnGeometryM, out double offsetM, out _))
            {
                return float.NaN;
            }

            return (float)offsetM;
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            if (!TryEvaluate(distanceOnGeometryM, out _, out double derivative))
            {
                return float.NaN;
            }

            return (float)derivative;
        }

        private bool TryEvaluate(float distanceOnGeometryM, out double offsetM, out double derivative)
        {
            offsetM = default;
            derivative = default;

            if (!Finite(startDistanceOnGeometryM) || !Finite(endDistanceOnGeometryM)
                || !Finite(distanceOnGeometryM) || !Finite(startOffsetM)
                || !Finite(radiusM) || !Finite(startHeadingDeg)
                || endDistanceOnGeometryM <= startDistanceOnGeometryM
                || distanceOnGeometryM < startDistanceOnGeometryM
                || distanceOnGeometryM > endDistanceOnGeometryM
                || Math.Abs(radiusM) < MinimumRadiusM || Math.Abs(startHeadingDeg) >= 90f)
            {
                return false;
            }

            double startHeadingRad = startHeadingDeg * Math.PI / 180.0;
            double sinHeading = Math.Sin(startHeadingRad)
                + ((double)distanceOnGeometryM - startDistanceOnGeometryM) / radiusM;
            if (sinHeading < -1.0 - DomainTolerance || sinHeading > 1.0 + DomainTolerance)
            {
                return false;
            }

            sinHeading = Math.Max(-1.0, Math.Min(1.0, sinHeading));
            double cosHeading = Math.Sqrt(1.0 - sinHeading * sinHeading);
            if (cosHeading <= 0.0)
            {
                return false;
            }

            offsetM = startOffsetM + radiusM * (Math.Cos(startHeadingRad) - cosHeading);
            derivative = sinHeading / cosHeading;

            return !double.IsNaN(offsetM) && !double.IsInfinity(offsetM)
                && !double.IsNaN(derivative) && !double.IsInfinity(derivative);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
