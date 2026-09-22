using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge
{
    [CreateAssetMenu(fileName = "TrackEdge", menuName = "Nakatetsu/Track/Edge")]
    public sealed class TrackEdgeAsset : ScriptableObject
    {
        [SerializeField] private TrackEdgeDefinition definition = new();

        /// <summary>Authoring data and baked distance map. Do not modify during simulation.</summary>
        public TrackEdgeDefinition Definition => definition;
    }
}
