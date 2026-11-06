using System;
using UnityEngine;

namespace Nakatetsu.World.Background
{
    public enum GuideLineYMode
    {
        TrackRelative = 0,
        ConstantWorldY = 1
    }

    [Serializable]
    public sealed class GuideLineDefinition
    {
        [SerializeField, Tooltip("参照Edge内で一意のGuideLine ID。")]
        private string id = string.Empty;

        [SerializeField, Tooltip("基準にするEdgeのID。")]
        private string edgeId = string.Empty;

        [SerializeField, Tooltip("EdgeのA→B方向を向いて右が正の横オフセット [m]。")]
        private float xOffsetM;

        [SerializeField] private GuideLineYMode yMode = GuideLineYMode.TrackRelative;

        [SerializeField, Tooltip("TrackRelativeでは断面内の高さオフセット、ConstantWorldYでは目標のワールドY座標 [m]。")]
        private float yValueM;

        [SerializeField, Tooltip("点計算時の横断面の座標軸にカントを反映する。")]
        private bool followCant = false;

        [SerializeField, Tooltip("点計算時の横断面の座標軸に勾配を反映する。")]
        private bool followGradient = true;

        public string Id => id;
        public string EdgeId => edgeId;
        public float XOffsetM => xOffsetM;
        public GuideLineYMode YMode => yMode;
        public float YValueM => yValueM;
        public bool FollowCant => followCant;
        public bool FollowGradient => followGradient;
    }
}
