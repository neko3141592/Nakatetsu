using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public sealed class BackgroundVertexMeshDefinition
    {
        [SerializeField, Tooltip("凸多角形の外周を一周する順に指定する頂点ID。")]
        private List<string> vertexIds = new();

        [SerializeField] private Material material;

        [SerializeField, Tooltip("頂点の並びで決まる面の向きを反転する。")]
        private bool reverseWinding;

        public IReadOnlyList<string> VertexIds => vertexIds;
        public Material Material => material;
        public bool ReverseWinding => reverseWinding;
    }
}
