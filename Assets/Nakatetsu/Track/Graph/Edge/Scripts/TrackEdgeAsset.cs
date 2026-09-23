using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge
{
    [CreateAssetMenu(fileName = "TrackEdge", menuName = "Nakatetsu/Track/Edge")]
    public sealed class TrackEdgeAsset : ScriptableObject
    {
        [SerializeField] private TrackEdgeDefinition definition = new();

        // 線路定義と生成済み距離表。走行中は変更しない。
        public TrackEdgeDefinition Definition => definition;
    }
}
