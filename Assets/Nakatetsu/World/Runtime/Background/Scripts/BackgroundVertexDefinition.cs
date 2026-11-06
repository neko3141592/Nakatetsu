using System;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public sealed class BackgroundVertexDefinition
    {
        [SerializeField] private string vertexId = string.Empty;
        [SerializeField] private string guideLineId = string.Empty;

        [SerializeField, Tooltip("参照Edgeの基準Geometry上の距離 [m]。")]
        private float distanceOnGeometryM;

        public string VertexId => vertexId;
        public string GuideLineId => guideLineId;
        public float DistanceOnGeometryM => distanceOnGeometryM;
    }
}
