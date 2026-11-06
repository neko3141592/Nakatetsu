using System;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    [Serializable]
    public abstract class BackgroundPrefabPlacementRule
    {
        [SerializeField, Tooltip("このアセット内のGuideLine ID。")]
        private string guideLineId = string.Empty;

        [SerializeField] private GameObject prefab;

        [SerializeField, Tooltip("接線に合わせた向きに加えるローカル回転 [度]。")]
        private Vector3 rotationOffsetEulerDeg;

        [SerializeField, Tooltip("Prefabの向きをGuideLineの勾配に合わせる。")]
        private bool followGradient = true;

        public string GuideLineId => guideLineId;
        public GameObject Prefab => prefab;
        public Vector3 RotationOffsetEulerDeg => rotationOffsetEulerDeg;
        public bool FollowGradient => followGradient;
    }
}
