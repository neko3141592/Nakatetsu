using UnityEngine;

namespace Nakatetsu.Track.Graph.Circuit
{
    [CreateAssetMenu(fileName = "TrackCircuit", menuName = "Nakatetsu/Track/Circuit")]
    public sealed class TrackCircuitAsset : ScriptableObject
    {
        [SerializeField] private TrackCircuitDefinition definition = new TrackCircuitDefinition();

        public TrackCircuitDefinition Definition => definition;
    }
}
