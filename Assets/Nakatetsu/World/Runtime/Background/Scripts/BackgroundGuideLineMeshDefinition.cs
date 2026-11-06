using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public sealed class BackgroundGuideLineMeshDefinition
    {
        [SerializeField, Tooltip("断面に沿って並べたGuideLine ID。隣り合う2本の間に面を作る。")]
        private List<string> guideLineIds = new();

        [SerializeField, Tooltip("共通Geometry上の開始距離 [m]。")]
        private float startDistanceOnGeometryM;

        [SerializeField, Tooltip("共通Geometry上の終了距離 [m]。")]
        private float endDistanceOnGeometryM;

        [SerializeField, Tooltip("Geometry上で断面をサンプリングする間隔 [m]。終了地点も含める。")]
        private float sampleIntervalM = 1f;

        [SerializeField] private Material material;

        [SerializeField, Tooltip("GuideLineの並びで決まる面の向きを反転する。")]
        private bool reverseWinding;

        public IReadOnlyList<string> GuideLineIds => guideLineIds;
        public float StartDistanceOnGeometryM => startDistanceOnGeometryM;
        public float EndDistanceOnGeometryM => endDistanceOnGeometryM;
        public float SampleIntervalM => sampleIntervalM;
        public Material Material => material;
        public bool ReverseWinding => reverseWinding;
    }
}
