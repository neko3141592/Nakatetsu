using System.Collections.Generic;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;

namespace Nakatetsu.Track.Graph
{
    public static class TrackGraphCompiler
    {
        // 全Edgeの距離表を一時領域に作る。Assetへの反映はEditor側で行う。
        public static bool TryBuildDistanceMaps(TrackGraphDefinition definition, float integrationStepM,
            out Dictionary<string, List<TrackEdgeDistanceSample>> distanceMaps, List<string> errors)
        {
            distanceMaps = null;
            var context = new TrackGraphContext();
            if (!TryCompile(definition, context, errors))
            {
                return false;
            }

            if (float.IsNaN(integrationStepM) || float.IsInfinity(integrationStepM) || integrationStepM <= 0f)
            {
                errors.Add("Integration step must be finite and positive.");
                return false;
            }

            var results = new Dictionary<string, List<TrackEdgeDistanceSample>>();
            foreach (var edge in definition.edges)
            {
                context.TryGetGeometry(edge.geometryId, out var geometry);
                if (!TrackEdgeCompiler.TryBuildDistanceMap(edge, geometry, integrationStepM, out var map, out var error))
                {
                    errors.Add($"Edge '{edge.edgeId}': {error}");
                    return false;
                }

                results.Add(edge.edgeId, map);
            }

            distanceMaps = results;
            return true;
        }

        public static bool TryCompile(TrackGraphDefinition definition, TrackGraphContext context, List<string> errors)
        {
            if (errors == null)
            {
                return false;
            }

            errors.Clear();
            if (definition == null)
            {
                errors.Add("Track graph definition is null.");
                return false;
            }

            if (definition.nodes == null)
            {
                errors.Add("Track graph nodes are null.");
            }

            if (definition.edges == null)
            {
                errors.Add("Track graph edges are null.");
            }

            if (definition.geometries == null)
            {
                errors.Add("Track graph geometries are null.");
            }

            if (context == null)
            {
                errors.Add("Track graph context is null.");
            }

            var geometriesById = new Dictionary<string, TrackGeometryDefinition>();
            if (definition.geometries != null)
            {
                for (var i = 0; i < definition.geometries.Count; i++)
                {
                    var geometry = definition.geometries[i];
                    if (geometry == null)
                    {
                        errors.Add($"geometries[{i}] is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(geometry.trackGeometryId))
                    {
                        errors.Add($"geometries[{i}] has an empty trackGeometryId.");
                        continue;
                    }

                    if (!geometriesById.TryAdd(geometry.trackGeometryId, geometry))
                    {
                        errors.Add($"Duplicate geometryId '{geometry.trackGeometryId}'.");
                    }

                    if (!IsFinite(geometry.lengthM) || geometry.lengthM <= 0f)
                    {
                        errors.Add($"Geometry '{geometry.trackGeometryId}' has an invalid length.");
                    }
                }
            }

            var nodeIds = new HashSet<string>();
            if (definition.nodes != null)
            {
                for (var i = 0; i < definition.nodes.Count; i++)
                {
                    var node = definition.nodes[i];
                    if (node == null)
                    {
                        errors.Add($"nodes[{i}] is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.nodeId))
                    {
                        errors.Add($"nodes[{i}] has an empty nodeId.");
                        continue;
                    }

                    if (!nodeIds.Add(node.nodeId))
                    {
                        errors.Add($"Duplicate nodeId '{node.nodeId}'.");
                    }
                }
            }

            var edgeIds = new HashSet<string>();
            if (definition.edges != null)
            {
                for (var i = 0; i < definition.edges.Count; i++)
                {
                    var edge = definition.edges[i];
                    if (edge == null)
                    {
                        errors.Add($"edges[{i}] is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(edge.edgeId))
                    {
                        errors.Add($"edges[{i}] has an empty edgeId.");
                    }
                    else if (!edgeIds.Add(edge.edgeId))
                    {
                        errors.Add($"Duplicate edgeId '{edge.edgeId}'.");
                    }

                    if (!nodeIds.Contains(edge.nodeAId))
                    {
                        errors.Add($"Edge '{edge.edgeId}' references missing nodeA '{edge.nodeAId}'.");
                    }

                    if (!nodeIds.Contains(edge.nodeBId))
                    {
                        errors.Add($"Edge '{edge.edgeId}' references missing nodeB '{edge.nodeBId}'.");
                    }

                    TrackGeometryDefinition geometry = null;
                    if (string.IsNullOrWhiteSpace(edge.geometryId))
                    {
                        errors.Add($"Edge '{edge.edgeId}' has an empty geometryId.");
                    }
                    else if (!geometriesById.TryGetValue(edge.geometryId, out geometry))
                    {
                        errors.Add($"Edge '{edge.edgeId}' references missing geometry '{edge.geometryId}'.");
                    }

                    float start = edge.startDistanceOnGeometryM;
                    float end = edge.endDistanceOnGeometryM;
                    if (!IsFinite(start) || !IsFinite(end) || start < 0f || end < 0f || start == end)
                    {
                        errors.Add($"Edge '{edge.edgeId}' has an invalid TrackGeometry distance range.");
                    }
                    else if (geometry != null && (start > geometry.lengthM || end > geometry.lengthM))
                    {
                        errors.Add($"Edge '{edge.edgeId}' exceeds geometry '{edge.geometryId}' distance range.");
                    }
                }
            }

            ValidateNodeEdgeLists(definition, errors);
            if (errors.Count != 0)
            {
                return false;
            }

            // 接続のIDとNodeごとの一意性を確認してから、固定・定位・反位のペアを検証する。
            var indexedGraph = new TrackGraphContext();
            if (!indexedGraph.TryBuildLookups(definition, out string lookupError))
            {
                errors.Add(lookupError);
                return false;
            }
            foreach (var connection in definition.connections)
            {
                if (!TrackConnectionValidator.TryValidate(connection, indexedGraph, out string error))
                {
                    errors.Add(error);
                }
            }

            if (errors.Count != 0)
            {
                return false;
            }

            context.Rebuild(definition);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateNodeEdgeLists(TrackGraphDefinition definition, List<string> errors)
        {
            if (definition.nodes == null || definition.edges == null)
            {
                return;
            }

            var edgeById = new Dictionary<string, TrackEdgeDefinition>();
            foreach (var edge in definition.edges)
            {
                if (edge != null && !string.IsNullOrWhiteSpace(edge.edgeId) && !edgeById.ContainsKey(edge.edgeId))
                {
                    edgeById.Add(edge.edgeId, edge);
                }
            }

            foreach (var node in definition.nodes)
            {
                if (node == null || node.connectedEdgeIds == null)
                {
                    continue;
                }

                var listed = new HashSet<string>();
                foreach (var edgeId in node.connectedEdgeIds)
                {
                    if (string.IsNullOrWhiteSpace(edgeId))
                    {
                        errors.Add($"Node '{node.nodeId}' lists an empty edgeId.");
                        continue;
                    }

                    if (!listed.Add(edgeId))
                    {
                        errors.Add($"Node '{node.nodeId}' lists edge '{edgeId}' more than once.");
                    }

                    if (!edgeById.TryGetValue(edgeId, out var edge))
                    {
                        errors.Add($"Node '{node.nodeId}' references missing edge '{edgeId}'.");
                        continue;
                    }

                    if (edge.nodeAId != node.nodeId && edge.nodeBId != node.nodeId)
                    {
                        errors.Add($"Node '{node.nodeId}' lists edge '{edgeId}', but is not one of its endpoints.");
                    }
                }
            }

            foreach (var edge in edgeById.Values)
            {
                RequireListed(definition, edge.nodeAId, edge.edgeId, errors);
                RequireListed(definition, edge.nodeBId, edge.edgeId, errors);
            }
        }

        private static void RequireListed(TrackGraphDefinition definition, string nodeId, string edgeId, List<string> errors)
        {
            var node = definition.nodes.Find(n => n != null && n.nodeId == nodeId);
            if (node == null || node.connectedEdgeIds == null || !node.connectedEdgeIds.Contains(edgeId))
            {
                errors.Add($"Edge '{edgeId}' is not listed by endpoint node '{nodeId}'.");
            }
        }
    }
}
