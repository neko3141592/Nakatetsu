using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Edge;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nakatetsu.World.Background
{
    public static class BackgroundGuideLineMeshBuilder
    {
        private const int MaxSampleRows = 100001;
        private const int MaxVertexCount = 1000000;

        public static bool TryBuild(TrackGraphContext graph, BackgroundDefinitionAsset asset,
            BackgroundGuideLineMeshDefinition definition, Matrix4x4 worldToLocal, out Mesh mesh, out string error)
        {
            mesh = null;
            if (graph == null || asset == null || definition?.GuideLineIds == null || definition.GuideLineIds.Count < 2)
            {
                error = "A GuideLine mesh requires a graph, a background asset, and at least two GuideLine IDs.";
                return false;
            }

            float startM = definition.StartDistanceOnGeometryM;
            float endM = definition.EndDistanceOnGeometryM;
            if (!TryGetSampleDistances(startM, endM, definition.SampleIntervalM, out var distances, out error))
            {
                return false;
            }

            int stripCount = definition.GuideLineIds.Count - 1;
            long vertexCount = (long)distances.Count * stripCount * 2;
            if (vertexCount > MaxVertexCount)
            {
                error = $"The GuideLine mesh would exceed {MaxVertexCount} vertices.";
                return false;
            }

            if (!TryResolveGuideLines(graph, asset, definition.GuideLineIds, startM, endM, out var guideLines,
                out var edges, out error))
            {
                return false;
            }

            var vertices = new Vector3[(int)vertexCount];
            var uv = new Vector2[vertices.Length];
            int rowStride = stripCount * 2;
            for (int row = 0; row < distances.Count; row++)
            {
                float distanceM = distances[row];
                float v = distanceM - startM;
                float u = 0f;
                Vector3 previousWorld = default;
                Vector3 previousLocal = default;
                for (int i = 0; i < guideLines.Length; i++)
                {
                    if (!edges[i].TryConvertToEdgeDistance(distanceM, out float distanceOnEdgeM)
                        || !GuideLineCalculator.TryEvaluatePoint(graph, guideLines[i], distanceOnEdgeM, out Vector3 worldPoint))
                    {
                        error = $"Could not evaluate GuideLine '{guideLines[i].Id}' at Geometry distance {distanceM} m.";
                        return false;
                    }

                    Vector3 localPoint = worldToLocal.MultiplyPoint3x4(worldPoint);
                    if (!IsFinite(localPoint))
                    {
                        error = $"Could not convert GuideLine '{guideLines[i].Id}' to mesh-local coordinates.";
                        return false;
                    }

                    if (i > 0)
                    {
                        float nextU = u + Vector3.Distance(previousWorld, worldPoint);
                        if (!float.IsFinite(nextU))
                        {
                            error = $"The cross-section width at Geometry distance {distanceM} m is invalid.";
                            return false;
                        }

                        // Each adjacent pair owns its vertices. This keeps a hard edge between strips.
                        int left = row * rowStride + (i - 1) * 2;
                        vertices[left] = previousLocal;
                        vertices[left + 1] = localPoint;
                        uv[left] = new Vector2(u, v);
                        uv[left + 1] = new Vector2(nextU, v);
                        u = nextU;
                    }

                    previousWorld = worldPoint;
                    previousLocal = localPoint;
                }
            }

            var triangles = new int[(distances.Count - 1) * stripCount * 6];
            int triangleIndex = 0;
            for (int row = 0; row < distances.Count - 1; row++)
            {
                for (int strip = 0; strip < stripCount; strip++)
                {
                    int left = row * rowStride + strip * 2;
                    int right = left + 1;
                    int nextLeft = left + rowStride;
                    int nextRight = nextLeft + 1;
                    triangles[triangleIndex++] = left;
                    triangles[triangleIndex++] = definition.ReverseWinding ? nextLeft : right;
                    triangles[triangleIndex++] = definition.ReverseWinding ? right : nextLeft;
                    triangles[triangleIndex++] = right;
                    triangles[triangleIndex++] = definition.ReverseWinding ? nextLeft : nextRight;
                    triangles[triangleIndex++] = definition.ReverseWinding ? nextRight : nextLeft;
                }
            }

            mesh = new Mesh { name = "Background GuideLine Mesh" };
            if (vertices.Length > ushort.MaxValue)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            error = null;
            return true;
        }

        private static bool TryGetSampleDistances(float startM, float endM, float intervalM,
            out List<float> distances, out string error)
        {
            distances = null;
            if (!float.IsFinite(startM) || !float.IsFinite(endM) || startM < 0f || endM <= startM
                || !float.IsFinite(intervalM) || intervalM <= 0f)
            {
                error = "Specify an ascending Geometry range and a positive finite sample interval.";
                return false;
            }

            double segmentCount = Math.Ceiling(((double)endM - startM) / intervalM);
            if (segmentCount >= MaxSampleRows)
            {
                error = $"The GuideLine mesh would exceed {MaxSampleRows} sample rows.";
                return false;
            }

            distances = new List<float>((int)segmentCount + 1) { startM };
            for (int i = 1; i < segmentCount; i++)
            {
                float distanceM = (float)(startM + i * (double)intervalM);
                if (distanceM >= endM)
                {
                    break;
                }

                if (distanceM <= distances[^1])
                {
                    error = "The sample interval is too small to advance the Geometry distance.";
                    return false;
                }

                distances.Add(distanceM);
            }

            distances.Add(endM);
            error = null;
            return true;
        }

        private static bool TryResolveGuideLines(TrackGraphContext graph, BackgroundDefinitionAsset asset,
            IReadOnlyList<string> ids, float startM, float endM, out GuideLineDefinition[] guideLines,
            out TrackEdgeDefinition[] edges, out string error)
        {
            guideLines = new GuideLineDefinition[ids.Count];
            edges = new TrackEdgeDefinition[ids.Count];
            string geometryId = null;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!GuideLineCalculator.TryFindDefinition(asset.GuideLines, id, out guideLines[i]))
                {
                    error = $"GuideLine ID '{id}' was not found or is ambiguous.";
                    return false;
                }

                if (!graph.TryGetEdge(guideLines[i].EdgeId, out var edge)
                    || !graph.TryGetGeometry(edge.geometryId, out _))
                {
                    error = $"GuideLine '{id}' has an invalid Edge or Geometry reference.";
                    return false;
                }

                geometryId ??= edge.geometryId;
                if (!string.Equals(geometryId, edge.geometryId, StringComparison.Ordinal))
                {
                    error = $"GuideLine '{id}' must use Geometry '{geometryId}' like the first GuideLine.";
                    return false;
                }

                float edgeStartM = Mathf.Min(edge.startDistanceOnGeometryM, edge.endDistanceOnGeometryM);
                float edgeEndM = Mathf.Max(edge.startDistanceOnGeometryM, edge.endDistanceOnGeometryM);
                if (!float.IsFinite(edgeStartM) || !float.IsFinite(edgeEndM) || startM < edgeStartM || endM > edgeEndM)
                {
                    error = $"GuideLine '{id}' cannot cover the requested Geometry range [{startM}, {endM}] m. "
                        + $"Available Geometry range: [{edgeStartM}, {edgeEndM}] m.";
                    return false;
                }

                edges[i] = edge;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
