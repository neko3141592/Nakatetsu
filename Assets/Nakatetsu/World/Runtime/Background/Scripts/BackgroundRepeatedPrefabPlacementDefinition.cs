using System;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public sealed class BackgroundRepeatedPrefabPlacementDefinition : BackgroundPrefabPlacementRule
    {
        [SerializeField, Tooltip("Node Aから配置開始地点までのEdge実距離 [m]。")]
        private float startDistanceOnEdgeM;

        [SerializeField, Tooltip("Node Aから配置終了地点までのEdge実距離 [m]。")]
        private float endDistanceOnEdgeM;

        [SerializeField, Tooltip("Edge実距離に沿った配置間隔 [m]。")]
        private float intervalM = 5f;

        public float StartDistanceOnEdgeM => startDistanceOnEdgeM;
        public float EndDistanceOnEdgeM => endDistanceOnEdgeM;
        public float IntervalM => intervalM;
    }
}
