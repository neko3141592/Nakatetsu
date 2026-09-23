using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track.Graph.Node
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph", sourceAssembly: "Nakatetsu.Track.Graph", sourceClassName: "TrackNodeDefinition")]
    public sealed class TrackNodeDefinition
    {
        public string nodeId;
        public List<string> connectedEdgeIds = new();
    }
}
