using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;

namespace Nakatetsu.Track.Graph
{
    // 実行時のID検索を保持する。参照先の定義は走行中に変更しない。
    public sealed class TrackGraphContext
    {
        private readonly Dictionary<string, TrackNodeDefinition> nodesById = new();
        private readonly Dictionary<string, TrackEdgeDefinition> edgesById = new();
        private readonly Dictionary<string, TrackGeometryDefinition> geometriesById = new();
        private readonly Dictionary<string, TrackConnectionDefinition> connectionsById = new();
        private readonly Dictionary<string, TrackConnectionDefinition> connectionsByNodeId = new();

        public IEnumerable<TrackConnectionDefinition> Connections => connectionsById.Values;

        // ID検索の構築結果。接続や移動データの妥当性は示さない。
        public bool IsInitialized { get; private set; }

        public bool TryGetNode(string id, out TrackNodeDefinition node)
        {
            node = null;
            return !string.IsNullOrWhiteSpace(id) && nodesById.TryGetValue(id, out node);
        }

        public bool TryGetEdge(string id, out TrackEdgeDefinition edge)
        {
            edge = null;
            return !string.IsNullOrWhiteSpace(id) && edgesById.TryGetValue(id, out edge);
        }

        public bool TryGetGeometry(string id, out TrackGeometryDefinition geometry)
        {
            geometry = null;
            return !string.IsNullOrWhiteSpace(id) && geometriesById.TryGetValue(id, out geometry);
        }

        public bool TryGetConnection(string id, out TrackConnectionDefinition connection)
        {
            connection = null;
            return !string.IsNullOrWhiteSpace(id) && connectionsById.TryGetValue(id, out connection);
        }

        public bool TryGetConnectionAtNode(string nodeId, out TrackConnectionDefinition connection)
        {
            connection = null;
            return !string.IsNullOrWhiteSpace(nodeId) && connectionsByNodeId.TryGetValue(nodeId, out connection);
        }

        // 定義をIDで検索できるようにする。線形評価や距離表の生成は行わない。
        public bool TryBuildLookups(TrackGraphDefinition definition, out string error)
        {
            ClearLookups();
            if (definition == null)
            {
                error = "Track graph definition is null.";
                return false;
            }

            if (!TryIndex(definition.nodes, nodesById, node => node.nodeId, "nodes", out error) ||
                !TryIndex(definition.edges, edgesById, edge => edge.edgeId, "edges", out error) ||
                !TryIndex(definition.geometries, geometriesById, geometry => geometry.trackGeometryId, "geometries", out error) ||
                !TryIndex(definition.connections, connectionsById, connection => connection.connectionId, "connections", out error) ||
                !TryIndex(definition.connections, connectionsByNodeId, connection => connection.nodeId, "connection nodes", out error))
            {
                ClearLookups();
                return false;
            }

            IsInitialized = true;
            error = null;
            return true;
        }

        // Compilerからの呼び出しでは、事前に定義の検証を終えている。
        internal void Rebuild(TrackGraphDefinition definition)
        {
            TryBuildLookups(definition, out _);
        }

        private void ClearLookups()
        {
            IsInitialized = false;
            nodesById.Clear();
            edgesById.Clear();
            geometriesById.Clear();
            connectionsById.Clear();
            connectionsByNodeId.Clear();
        }

        private static bool TryIndex<T>(List<T> entries, Dictionary<string, T> lookup,
            Func<T, string> getId, string kind, out string error) where T : class
        {
            if (entries == null)
            {
                error = $"Track graph {kind} are null.";
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    error = $"{kind}[{i}] is null.";
                    return false;
                }

                string id = getId(entry);
                if (string.IsNullOrWhiteSpace(id))
                {
                    error = $"{kind}[{i}] has an empty ID.";
                    return false;
                }

                if (!lookup.TryAdd(id, entry))
                {
                    error = $"Duplicate ID '{id}' in {kind}.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
