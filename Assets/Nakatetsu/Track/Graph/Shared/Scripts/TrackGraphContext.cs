using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;

namespace Nakatetsu.Track.Graph
{
    /// <summary>Runtime ID lookups. Referenced authoring definitions must not be modified during simulation.</summary>
    public sealed class TrackGraphContext
    {
        private readonly Dictionary<string, TrackNodeDefinition> nodesById = new();
        private readonly Dictionary<string, TrackEdgeDefinition> edgesById = new();
        private readonly Dictionary<string, TrackGeometryDefinition> geometriesById = new();

        /// <summary>Only indicates successful ID indexing, not validated topology or movement data.</summary>
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

        /// <summary>Builds ID lookups only. Does not evaluate geometry, generate LUTs or validate connections.</summary>
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
                !TryIndex(definition.geometries, geometriesById, geometry => geometry.trackGeometryId, "geometries", out error))
            {
                ClearLookups();
                return false;
            }

            IsInitialized = true;
            error = null;
            return true;
        }

        // Existing Compiler entry point; its caller has already validated the definition.
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
