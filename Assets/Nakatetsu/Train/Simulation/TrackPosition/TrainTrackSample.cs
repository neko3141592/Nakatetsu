using Nakatetsu.Track;
using UnityEngine;

namespace Nakatetsu.Train.Simulation.TrackPosition
{
    // 編成上の取付位置に対応する線路サンプル。向きと勾配は編成前方向を基準とする。
    public readonly struct TrainTrackSample
    {
        public int CarIndex { get; }
        public float OffsetFromCarCenterM { get; }
        public string EdgeId { get; }

        // Node A起点のEdge実距離[m]。
        public float DistanceOnEdgeM { get; }
        public bool FrontFacesAtoB { get; }
        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Quaternion Rotation { get; }
        public float GradientPermille { get; }

        internal TrainTrackSample(int carIndex, float offsetFromCarCenterM,
            string edgeId, bool frontFacesAtoB, TrackSample trackSample)
        {
            CarIndex = carIndex;
            OffsetFromCarCenterM = offsetFromCarCenterM;
            EdgeId = edgeId;
            DistanceOnEdgeM = trackSample.DistanceM;
            FrontFacesAtoB = frontFacesAtoB;
            Position = trackSample.Position;
            Tangent = frontFacesAtoB ? trackSample.Tangent : -trackSample.Tangent;
            Rotation = frontFacesAtoB ? trackSample.Rotation
                : trackSample.Rotation * Quaternion.AngleAxis(180f, Vector3.up);
            GradientPermille = frontFacesAtoB ? trackSample.GradientPermille : -trackSample.GradientPermille;
        }
    }
}
