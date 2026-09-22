using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Nakatetsu.Track
{
    [MovedFrom(true, sourceNamespace: "Nakatetsu.Track.Graph.Geometry", sourceAssembly: "Nakatetsu.Track", sourceClassName: "TrackGeometrySample")]
    public readonly struct TrackSample
    {
        /// <summary>
        /// 評価した地点の距離[m]。Geometryでは起点からの基準線距離、EdgeではNode Aからの実距離を表す。
        /// 評価時に距離をClampした場合は、補正後の距離を保持する。
        /// </summary>
        public float DistanceM { get; }

        /// <summary>評価地点のワールド座標[m]。原点の位置・方向と高さを反映した位置。</summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 距離が増える向きのワールド空間の単位方向ベクトル。列車の移動方向とは独立する。
        /// 現在のGeometry評価ではRotationから生成しており、位置の解析微分そのものではない。
        /// </summary>
        public Vector3 Tangent { get; }

        /// <summary>
        /// 評価地点のワールド空間の姿勢。ローカル+Zを線路の前方向へ向ける回転。
        /// 現在のGeometry評価では水平角と勾配による傾きを反映し、カントは含まない。
        /// </summary>
        public Quaternion Rotation { get; }

        /// <summary>
        /// 評価地点の勾配[‰]。距離が増える向きに上る場合が正、下る場合が負。
        /// 現在のGeometry評価では、基準線距離に対する高さの微分dy/dSを1000倍した値。
        /// </summary>
        public float GradientPermille { get; }

        public TrackSample(float distanceM, Vector3 position, Vector3 tangent,
            Quaternion rotation, float gradientPermille)
        {
            DistanceM = distanceM;
            Position = position;
            Tangent = tangent;
            Rotation = rotation;
            GradientPermille = gradientPermille;
        }
    }
}
