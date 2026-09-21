using System.Collections.Generic;

namespace Nakatetsu.Track.Graph
{
    public static class TrackGraphCompiler
    {
        public static bool TryCompile(TrackGraphDefinition definition, TrackGraphContext context, List<string> errors)
        {
            if (errors == null) return false;
            errors.Clear();
            if (definition == null)
            {
                errors.Add("Track graph definition is null.");
                return false;
            }
            if (definition.nodes == null) errors.Add("Track graph nodes are null.");
            if (definition.edges == null) errors.Add("Track graph edges are null.");

            var nodeIds = new HashSet<string>();
            if (definition.nodes != null)
            {
                for (var i = 0; i < definition.nodes.Count; i++)
                {
                    var node = definition.nodes[i];
                    if (node == null) { errors.Add($"nodes[{i}] is null."); continue; }
                    if (string.IsNullOrWhiteSpace(node.nodeId)) { errors.Add($"nodes[{i}] has an empty nodeId."); continue; }
                    if (!nodeIds.Add(node.nodeId)) errors.Add($"Duplicate nodeId '{node.nodeId}'.");
                }
            }

            var edgeIds = new HashSet<string>();
            if (definition.edges != null)
            {
                for (var i = 0; i < definition.edges.Count; i++)
                {
                    var edge = definition.edges[i];
                    if (edge == null) { errors.Add($"edges[{i}] is null."); continue; }
                    if (string.IsNullOrWhiteSpace(edge.edgeId)) errors.Add($"edges[{i}] has an empty edgeId.");
                    else if (!edgeIds.Add(edge.edgeId)) errors.Add($"Duplicate edgeId '{edge.edgeId}'.");
                    if (!nodeIds.Contains(edge.nodeAId)) errors.Add($"Edge '{edge.edgeId}' references missing nodeA '{edge.nodeAId}'.");
                    if (!nodeIds.Contains(edge.nodeBId)) errors.Add($"Edge '{edge.edgeId}' references missing nodeB '{edge.nodeBId}'.");
                    if (string.IsNullOrWhiteSpace(edge.guideLineId)) errors.Add($"Edge '{edge.edgeId}' has an empty guideLineId.");
                    if (edge.startDistanceOnGuideLineM < 0f || edge.endDistanceOnGuideLineM < 0f || edge.LengthM <= 0f)
                        errors.Add($"Edge '{edge.edgeId}' has an invalid GuideLine distance range.");
                }
            }

            ValidateNodeEdgeLists(definition, errors);
            if (errors.Count != 0 || context == null) return false;
            context.Rebuild(definition);
            return true;
        }

        private static void ValidateNodeEdgeLists(TrackGraphDefinition definition, List<string> errors)
        {
            if (definition.nodes == null || definition.edges == null) return;
            var edgeById = new Dictionary<string, TrackEdgeDefinition>();
            foreach (var edge in definition.edges)
                if (edge != null && !string.IsNullOrWhiteSpace(edge.edgeId) && !edgeById.ContainsKey(edge.edgeId)) edgeById.Add(edge.edgeId, edge);

            foreach (var node in definition.nodes)
            {
                if (node == null || node.connectedEdgeIds == null) continue;
                var listed = new HashSet<string>();
                foreach (var edgeId in node.connectedEdgeIds)
                {
                    if (!listed.Add(edgeId)) errors.Add($"Node '{node.nodeId}' lists edge '{edgeId}' more than once.");
                    if (!edgeById.TryGetValue(edgeId, out var edge)) { errors.Add($"Node '{node.nodeId}' references missing edge '{edgeId}'."); continue; }
                    if (edge.nodeAId != node.nodeId && edge.nodeBId != node.nodeId)
                        errors.Add($"Node '{node.nodeId}' lists edge '{edgeId}', but is not one of its endpoints.");
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
                errors.Add($"Edge '{edgeId}' is not listed by endpoint node '{nodeId}'.");
        }
    }
}
