using System;

namespace Nakatetsu.Track.Graph.Edge
{
    // 基準線平面の曲率をGeometry距離に対して線形に変える緩和曲線。
    // 実距離をパラメーターとする厳密なクロソイドとは区別する。
    [Serializable]
    public abstract class TrackEdgeTransitionOffsetSegment : TrackEdgeOffsetSegment
    {
        public float startOffsetM;
        public float radiusM;
        public float startHeadingDeg;

        protected abstract bool IsTransitionIn { get; }

        public override float EvaluateOffsetM(float distanceOnGeometryM)
        {
            if (!TryParameters(distanceOnGeometryM, out double lengthM, out double localM, out double sine))
            {
                return float.NaN;
            }

            double slope0 = Slope(sine);
            double middleM = localM / 2.0;
            double slopeMiddle = Slope(SineAt(middleM, lengthM, sine));
            double slopeEnd = Slope(SineAt(localM, lengthM, sine));
            double integral = localM * (slope0 + 4.0 * slopeMiddle + slopeEnd) / 6.0;
            double toleranceM = 1e-9 * Math.Max(1.0, localM);
            if (!TryIntegrate(0.0, localM, slope0, slopeMiddle, slopeEnd, integral,
                    toleranceM, 18, lengthM, sine, out double displacementM))
            {
                return float.NaN;
            }

            return (float)(startOffsetM + displacementM);
        }

        public override float EvaluateDerivative(float distanceOnGeometryM)
        {
            return TryParameters(distanceOnGeometryM, out double lengthM, out double localM, out double sine)
                ? (float)Slope(SineAt(localM, lengthM, sine))
                : float.NaN;
        }

        // 直線Geometry上ではEdgeの平面曲率になる。符号は右曲がりが正。
        public float EvaluateCurvaturePerM(float distanceOnGeometryM)
        {
            if (!TryParameters(distanceOnGeometryM, out double lengthM, out double localM, out _))
            {
                return float.NaN;
            }

            double t = localM / lengthM;
            return (float)((IsTransitionIn ? t : 1.0 - t) / radiusM);
        }

        private bool TryParameters(float distance, out double lengthM, out double localM, out double sine)
        {
            lengthM = (double)endDistanceOnGeometryM - startDistanceOnGeometryM;
            localM = (double)distance - startDistanceOnGeometryM;
            sine = Math.Sin(startHeadingDeg * Math.PI / 180.0);

            if (!Finite(startDistanceOnGeometryM) || !Finite(endDistanceOnGeometryM)
                || !Finite(distance) || !Finite(startOffsetM) || !Finite(radiusM) || !Finite(startHeadingDeg)
                || startDistanceOnGeometryM < 0f || lengthM <= 0.0
                || localM < 0.0 || localM > lengthM || Math.Abs(radiusM) < 0.001
                || Math.Abs(startHeadingDeg) >= 90f)
            {
                return false;
            }

            // 両形式ともsin(接線角)は区間内で単調。全区間が横オフセットとして表せることを確認する。
            double endSine = sine + lengthM / (2.0 * radiusM);
            return Math.Abs(sine) < 1.0 && Math.Abs(endSine) < 1.0;
        }

        private double SineAt(double localM, double lengthM, double sine)
        {
            double t = localM / lengthM;
            return sine + localM * (IsTransitionIn ? t / 2.0 : 1.0 - t / 2.0) / radiusM;
        }

        private static double Slope(double sine) => sine / Math.Sqrt(1.0 - sine * sine);

        private bool TryIntegrate(double from, double to, double a, double middle, double b,
            double estimate, double tolerance, int remainingDepth, double lengthM, double sine, out double integral)
        {
            double center = (from + to) / 2.0;
            double leftMiddle = Slope(SineAt((from + center) / 2.0, lengthM, sine));
            double rightMiddle = Slope(SineAt((center + to) / 2.0, lengthM, sine));
            double left = (center - from) * (a + 4.0 * leftMiddle + middle) / 6.0;
            double right = (to - center) * (middle + 4.0 * rightMiddle + b) / 6.0;
            double difference = left + right - estimate;
            if (Math.Abs(difference) <= 15.0 * tolerance)
            {
                integral = left + right + difference / 15.0;
                return true;
            }

            integral = default;
            if (remainingDepth == 0
                || !TryIntegrate(from, center, a, leftMiddle, middle, left, tolerance / 2.0,
                    remainingDepth - 1, lengthM, sine, out double leftIntegral)
                || !TryIntegrate(center, to, middle, rightMiddle, b, right, tolerance / 2.0,
                    remainingDepth - 1, lengthM, sine, out double rightIntegral))
            {
                return false;
            }

            integral = leftIntegral + rightIntegral;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
