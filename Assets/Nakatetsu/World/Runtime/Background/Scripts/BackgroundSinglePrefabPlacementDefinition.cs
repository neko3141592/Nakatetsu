using System;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public sealed class BackgroundSinglePrefabPlacementDefinition : BackgroundPrefabPlacementRule
    {
        [SerializeField, Tooltip("Node AからのEdge実距離 [m]。")]
        private float distanceOnEdgeM;

        public float DistanceOnEdgeM => distanceOnEdgeM;
    }
}
