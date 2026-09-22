using Nakatetsu.Track.Graph.Geometry;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge
{
    public static class TrackEdgeCalculator
    {
        public static bool TryEvaluate(
            TrackGraphContext graph,
            string currentEdgeId,
            float distanceOnEdgeM,
            out TrackSample sample
        )
        {
            sample = new TrackSample();

            if (graph == null || !graph.TryGetEdge(currentEdgeId, out TrackEdgeDefinition edge))
            {
                return false;
            }

            if (!graph.TryGetGeometry(edge.geometryId, out TrackGeometryDefinition geometry))
            {
                return false;
            }
            
            if (!edge.TryConvertToGeometryDistance(distanceOnEdgeM, out float distanceOnGeometryM))
            {
                return false;
            }

            if (!TryEvaluateAtGeometryDistance(edge, geometry, distanceOnGeometryM,
                out Vector3 edgePosition, out Vector3 edgePositionPrime)) return false;

            // 接線・勾配はEdge実距離が増えるNode A→B方向へそろえる。
            if (edge.endDistanceOnGeometryM < edge.startDistanceOnGeometryM)
                edgePositionPrime = -edgePositionPrime;

            float horizontalRate = new Vector2(edgePositionPrime.x, edgePositionPrime.z).magnitude;
            float derivativeMagnitude = edgePositionPrime.magnitude;
            if (!IsFinite(horizontalRate) || horizontalRate <= 1e-6f
                || !IsFinite(derivativeMagnitude) || derivativeMagnitude <= 1e-6f) return false;

            Vector3 tangent = edgePositionPrime / derivativeMagnitude;
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
            float gradientPermille = edgePositionPrime.y / horizontalRate * 1000f;
            if (!IsFinite(gradientPermille) || !IsFinite(rotation.x) || !IsFinite(rotation.y)
                || !IsFinite(rotation.z) || !IsFinite(rotation.w)) return false;

            sample = new TrackSample(distanceOnEdgeM, edgePosition, tangent, rotation, gradientPermille);
            return true;
        }

        /// <summary>LUTを使わずGeometry距離からワールド位置とdP/dSを評価する。微分はGeometry増加方向。</summary>
        public static bool TryEvaluateAtGeometryDistance(TrackEdgeDefinition edge, TrackGeometryDefinition geometry,
            float distanceOnGeometryM, out Vector3 position, out Vector3 edgeDerivative)
        {
            position = default;
            edgeDerivative = default;
            if (edge == null || geometry == null || edge.geometryId != geometry.trackGeometryId) return false;

            if (!edge.TryEvaluateOffsetAtGeometryDistance(distanceOnGeometryM, out float offsetM, out float offsetDerivative))
            {
                return false;
            }

            if (!geometry.TryEvaluateAtGeometryDistance(distanceOnGeometryM, out Vector3 geometryPosition, out Vector3 derivative, out Vector3 secondDerivative))
            {
                return false;
            }

            float q = Mathf.Sqrt(derivative.x * derivative.x + derivative.z * derivative.z);
            if (!IsFinite(q) || q <= 1e-6f)
            {
                return false;
            }

            Vector3 horizontalTangent = new Vector3(derivative.x, 0f, derivative.z) / q; // T(l)
            Vector3 right = Vector3.Cross(Vector3.up, horizontalTangent); // B(l)
            Vector3 edgePosition = geometryPosition + right * offsetM; // P(l) = G(l) + o(l)B(l)

            // P(l) = G'(l) + o(l)B'(l) + o'(l)B(l)
            Vector3 geometryPositionPrime = derivative; // G'(l)
            float offsetPrime = offsetDerivative; // o'(l)

            // B'(l) = (z''(l)/q - z'q'/q^2, 0, -x''/q + x'q'/q^2)
            // q = Sqrt(x'^2 + z'^2)
            float qPrime = (derivative.x * secondDerivative.x + derivative.z * secondDerivative.z) / q;
            Vector3 rightPrime = new Vector3(
                secondDerivative.z / q - derivative.z * qPrime / (q * q),
                0f,
                -secondDerivative.x / q + derivative.x * qPrime / (q * q)
            ); // B'(l)

            Vector3 edgePositionPrime = geometryPositionPrime + offsetM * rightPrime + offsetPrime * right;

            if (!IsFinite(edgePosition) || !IsFinite(edgePositionPrime)) return false;
            position = edgePosition;
            edgeDerivative = edgePositionPrime;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
