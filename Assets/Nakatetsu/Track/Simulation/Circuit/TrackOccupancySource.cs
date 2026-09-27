using System.Collections.Generic;

namespace Nakatetsu.Track.Simulation.Circuit
{
    // Geometry距離は区間の始点から終点へ進む順であり、逆方向なら減少する。
    public readonly struct TrackOccupiedEdgeSpan
    {
        public string EdgeId { get; }
        public float StartDistanceOnGeometryM { get; }
        public float EndDistanceOnGeometryM { get; }

        public TrackOccupiedEdgeSpan(string edgeId, float startDistanceOnGeometryM,
            float endDistanceOnGeometryM)
        {
            EdgeId = edgeId;
            StartDistanceOnGeometryM = startDistanceOnGeometryM;
            EndDistanceOnGeometryM = endDistanceOnGeometryM;
        }
    }

    // 軌道回路は列車型を知らず、この問い合わせ口だけから占有区間を読む。
    public interface ITrackOccupancySource
    {
        // 失敗時はdestinationを変更しない。失敗は「空き」を意味しない。
        bool TryGetOccupiedEdges(List<TrackOccupiedEdgeSpan> destination);
    }
}
