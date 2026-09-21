using UnityEngine;

namespace Nakatetsu.Track.Graph
{
    [CreateAssetMenu(fileName = "TrackGraph", menuName = "Nakatetsu/Track/Graph")]
    public sealed class TrackGraphAsset : ScriptableObject
    {
        [SerializeField] private TrackGraphDefinition definition = new TrackGraphDefinition();

        public TrackGraphDefinition Definition => definition;
    }
}
