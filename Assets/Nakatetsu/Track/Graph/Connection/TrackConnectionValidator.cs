namespace Nakatetsu.Track.Graph.Connection
{
    public static class TrackConnectionValidator
    {
        public static bool TryValidate(TrackConnectionDefinition connection, TrackGraphContext graph, out string error)
        {
            error = null;
            if (connection == null || string.IsNullOrWhiteSpace(connection.connectionId))
            {
                return Fail("Connection or connectionId is missing.", out error);
            }

            if (graph == null || !graph.TryGetNode(connection.nodeId, out var node))
            {
                return Fail($"Connection '{connection.connectionId}' references a missing Node.", out error);
            }

            var pairs = connection.edgePairs;
            if (pairs == null || (pairs.Count != 1 && pairs.Count != 2))
            {
                return Fail($"Connection '{connection.connectionId}' must contain one or two Edge pairs.", out error);
            }

            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.pairId))
                {
                    return Fail($"Connection '{connection.connectionId}' has a missing pair or pairId.", out error);
                }

                if (pair.edgeAId == pair.edgeBId)
                {
                    return Fail($"Pair '{pair.pairId}' must connect two different Edges.", out error);
                }

                if (!graph.TryGetEdge(pair.edgeAId, out var edgeA) ||
                    !graph.TryGetEdge(pair.edgeBId, out var edgeB))
                {
                    return Fail($"Pair '{pair.pairId}' references a missing Edge.", out error);
                }

                if ((edgeA.nodeAId != node.nodeId && edgeA.nodeBId != node.nodeId) ||
                    (edgeB.nodeAId != node.nodeId && edgeB.nodeBId != node.nodeId) ||
                    node.connectedEdgeIds == null || !node.connectedEdgeIds.Contains(pair.edgeAId) ||
                    !node.connectedEdgeIds.Contains(pair.edgeBId))
                {
                    return Fail($"Pair '{pair.pairId}' is not connected to Node '{node.nodeId}'.", out error);
                }
            }

            if (pairs.Count == 1)
            {
                if (pairs[0].condition != TrackConnectionCondition.Always)
                {
                    return Fail($"Fixed connection '{connection.connectionId}' must use Always.", out error);
                }

                return true;
            }

            var first = pairs[0];
            var second = pairs[1];
            if (first.pairId == second.pairId)
            {
                return Fail($"Connection '{connection.connectionId}' has duplicate pairIds.", out error);
            }

            if (!((first.condition == TrackConnectionCondition.Normal && second.condition == TrackConnectionCondition.Reverse) ||
                  (first.condition == TrackConnectionCondition.Reverse && second.condition == TrackConnectionCondition.Normal)))
            {
                return Fail($"Switch '{connection.connectionId}' must have one Normal and one Reverse pair.", out error);
            }

            // 転轍機の定位・反位のペアは、共通Edgeを1本だけ持つ。
            int commonEdges = 0;
            if (first.edgeAId == second.edgeAId || first.edgeAId == second.edgeBId)
            {
                commonEdges++;
            }

            if (first.edgeBId == second.edgeAId || first.edgeBId == second.edgeBId)
            {
                commonEdges++;
            }

            if (commonEdges != 1)
            {
                return Fail($"Switch '{connection.connectionId}' must have exactly one common Edge.", out error);
            }

            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
