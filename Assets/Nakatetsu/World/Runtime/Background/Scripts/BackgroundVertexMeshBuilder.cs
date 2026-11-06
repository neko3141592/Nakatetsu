using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nakatetsu.World.Background
{
    public static class BackgroundVertexMeshBuilder
    {
        private const float BoundaryToleranceM = 1e-5f;
        private const float PlaneToleranceM = 0.002f;

        public static bool TryBuild(TrackGraphContext graph, BackgroundDefinitionAsset asset,
            BackgroundVertexMeshDefinition definition, Matrix4x4 worldToLocal, out Mesh mesh, out string error)
        {
            mesh = null;
            if (asset == null || definition?.VertexIds == null || definition.VertexIds.Count < 3)
            {
                error = "A vertex mesh requires at least three boundary vertex IDs.";
                return false;
            }

            if (!TryEvaluateVertices(graph, asset, definition.VertexIds, out var worldPoints, out error)
                || !TryProjectPolygon(worldPoints, out var uv, out Vector3 axisX, out Vector3 axisY, out error)
                || !TryValidateBoundary(uv, out error))
            {
                return false;
            }

            var triangles = new List<int>((uv.Length - 2) * 3);
            for (int i = 1; i < uv.Length - 1; i++)
            {
                if (Cross(uv[i] - uv[0], uv[i + 1] - uv[0]) <= BoundaryToleranceM * BoundaryToleranceM)
                {
                    continue;
                }

                triangles.Add(0);
                triangles.Add(definition.ReverseWinding ? i + 1 : i);
                triangles.Add(definition.ReverseWinding ? i : i + 1);
            }

            Vector3 localNormal = Vector3.Cross(worldToLocal.MultiplyVector(axisX),
                worldToLocal.MultiplyVector(axisY)).normalized;
            if (triangles.Count == 0 || !IsFinite(localNormal) || localNormal == Vector3.zero)
            {
                error = "The vertex mesh has zero area.";
                return false;
            }

            var localPoints = new Vector3[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                localPoints[i] = worldToLocal.MultiplyPoint3x4(worldPoints[i]);
                if (!IsFinite(localPoints[i]))
                {
                    error = "A vertex could not be converted to mesh-local coordinates.";
                    return false;
                }
            }

            var normals = new Vector3[localPoints.Length];
            Array.Fill(normals, definition.ReverseWinding ? -localNormal : localNormal);
            mesh = new Mesh { name = "Background Vertex Mesh" };
            if (localPoints.Length > ushort.MaxValue)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = localPoints;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
            error = null;
            return true;
        }

        private static bool TryEvaluateVertices(TrackGraphContext graph, BackgroundDefinitionAsset asset,
            IReadOnlyList<string> vertexIds, out Vector3[] points, out string error)
        {
            points = new Vector3[vertexIds.Count];
            for (int i = 0; i < vertexIds.Count; i++)
            {
                string vertexId = vertexIds[i];
                if (string.IsNullOrWhiteSpace(vertexId))
                {
                    error = $"Vertex mesh boundary entry {i + 1} requires a vertex ID.";
                    return false;
                }

                BackgroundVertexDefinition vertex = null;
                foreach (BackgroundVertexDefinition candidate in asset.Vertices)
                {
                    if (candidate == null || !string.Equals(candidate.VertexId, vertexId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (vertex != null)
                    {
                        error = $"Vertex ID '{vertexId}' is ambiguous.";
                        return false;
                    }

                    vertex = candidate;
                }

                if (vertex == null)
                {
                    error = $"Vertex ID '{vertexId}' was not found.";
                    return false;
                }

                if (!BackgroundVertexCalculator.TryEvaluatePoint(graph, asset.GuideLines, vertex, out points[i]))
                {
                    error = $"Could not evaluate vertex '{vertexId}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryProjectPolygon(Vector3[] points, out Vector2[] uv, out Vector3 axisX,
            out Vector3 axisY, out string error)
        {
            uv = null;
            axisY = default;
            Vector3 origin = points[0];
            axisX = (points[1] - origin).normalized;
            if (axisX == Vector3.zero)
            {
                error = "The polygon has coincident boundary vertices.";
                return false;
            }

            // Use the point farthest from the first edge so nearly collinear points do not set the plane.
            Vector3 planeCross = Vector3.zero;
            float largestArea = 0f;
            for (int i = 2; i < points.Length; i++)
            {
                Vector3 candidate = Vector3.Cross(axisX, points[i] - origin);
                float area = candidate.sqrMagnitude;
                if (area > largestArea)
                {
                    largestArea = area;
                    planeCross = candidate;
                }
            }

            Vector3 normal = planeCross.normalized;
            if (!IsFinite(normal) || normal == Vector3.zero)
            {
                error = "The vertex mesh has zero area.";
                return false;
            }

            axisY = Vector3.Cross(normal, axisX);
            uv = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 offset = points[i] - origin;
                float height = Vector3.Dot(normal, offset);
                if (!float.IsFinite(height) || Mathf.Abs(height) > PlaneToleranceM)
                {
                    error = "The polygon vertices must lie on one plane.";
                    return false;
                }

                float u = Vector3.Dot(offset, axisX);
                float v = Vector3.Dot(offset, axisY);
                if (!float.IsFinite(u) || !float.IsFinite(v))
                {
                    error = "A vertex could not be projected onto the polygon plane.";
                    return false;
                }

                uv[i] = new Vector2(u, v);
            }

            error = null;
            return true;
        }

        private static bool TryValidateBoundary(Vector2[] points, out string error)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 side = points[(i + 1) % points.Length] - points[i];
                float sideTolerance = BoundaryToleranceM * side.magnitude;
                for (int j = 0; j < points.Length; j++)
                {
                    Vector2 offset = points[j] - points[i];
                    if (j > i && offset.sqrMagnitude <= BoundaryToleranceM * BoundaryToleranceM)
                    {
                        error = "The polygon has coincident boundary vertices.";
                        return false;
                    }

                    // A convex ordered boundary keeps every point on the same side of every edge.
                    if (Cross(side, offset) < -sideTolerance)
                    {
                        error = "The vertex mesh boundary must be a simple convex polygon in perimeter order.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
