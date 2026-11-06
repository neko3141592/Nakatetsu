using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    public static class GuideLineCalculator
    {
        private const float TangentSampleOffsetM = 0.1f;
        private const float MinimumTangentDeltaM = 1e-5f;

        // Node AからのEdge実距離を指定し、GuideLineのワールド座標を求める。
        public static bool TryEvaluatePoint(TrackGraphContext graph, GuideLineDefinition guideLine,
            float distanceOnEdgeM, out Vector3 point)
        {
            point = default;

            // 現行のEdge評価ではカントを取得できない。
            if (guideLine == null || guideLine.FollowCant
                || !TrackEdgeCalculator.TryEvaluate(graph, guideLine.EdgeId, distanceOnEdgeM, out var edgeSample))
            {
                return false;
            }

            // カントを含まない姿勢では、勾配を切り替えても右方向は水平で共通。
            Vector3 right = edgeSample.Rotation * Vector3.right;
            Vector3 up = guideLine.FollowGradient ? edgeSample.Rotation * Vector3.up : Vector3.up;
            Vector3 pointAtX = edgeSample.Position + right * guideLine.XOffsetM;
            Vector3 candidate;

            switch (guideLine.YMode)
            {
                case GuideLineYMode.TrackRelative:
                    candidate = pointAtX + up * guideLine.YValueM;
                    break;

                case GuideLineYMode.ConstantWorldY:
                    if (Mathf.Abs(up.y) <= 1e-6f)
                    {
                        return false;
                    }

                    // 断面内の上方向に沿って、指定ワールドYの水平面と交わる点を求める。
                    float heightOnSectionM = (guideLine.YValueM - pointAtX.y) / up.y;
                    candidate = pointAtX + up * heightOnSectionM;
                    candidate.y = guideLine.YValueM;
                    break;

                default:
                    return false;
            }

            if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y) || !float.IsFinite(candidate.z))
            {
                return false;
            }

            point = candidate;
            return true;
        }

        // 指定距離の前後を評価し、Edge実距離が増える向きの接線を近似する。
        public static bool TryEvaluateTangent(TrackGraphContext graph, GuideLineDefinition guideLine,
            float distanceOnEdgeM, out Vector3 tangent)
        {
            tangent = default;

            if (graph == null || guideLine == null || !float.IsFinite(distanceOnEdgeM)
                || !graph.TryGetEdge(guideLine.EdgeId, out var edge))
            {
                return false;
            }

            float edgeLengthM = edge.LengthM;
            if (!float.IsFinite(edgeLengthM) || distanceOnEdgeM < 0f || distanceOnEdgeM > edgeLengthM)
            {
                return false;
            }

            // 端点の近くではEdge内の点を使い、端点ちょうどでは片側の差分になる。
            float beforeM = Mathf.Max(0f, distanceOnEdgeM - TangentSampleOffsetM);
            float afterM = Mathf.Min(edgeLengthM, distanceOnEdgeM + TangentSampleOffsetM);
            if (!TryEvaluatePoint(graph, guideLine, beforeM, out Vector3 before)
                || !TryEvaluatePoint(graph, guideLine, afterM, out Vector3 after))
            {
                return false;
            }

            Vector3 delta = after - before;
            float magnitude = delta.magnitude;
            if (!float.IsFinite(magnitude) || magnitude <= MinimumTangentDeltaM)
            {
                return false;
            }

            tangent = delta / magnitude;
            return true;
        }

        internal static bool TryFindDefinition(IReadOnlyList<GuideLineDefinition> guideLines, string id,
            out GuideLineDefinition guideLine)
        {
            guideLine = null;
            if (guideLines == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            foreach (GuideLineDefinition candidate in guideLines)
            {
                if (candidate == null || !string.Equals(candidate.Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (guideLine != null)
                {
                    guideLine = null;
                    return false;
                }

                guideLine = candidate;
            }

            return guideLine != null;
        }
    }
}
