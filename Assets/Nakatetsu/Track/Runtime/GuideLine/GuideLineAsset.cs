using UnityEngine;

namespace Nakatetsu.Track.GuideLine
{
    [CreateAssetMenu(fileName = "GuideLine", menuName = "Nakatetsu/Track/Guide Line")]
    public sealed class GuideLineAsset : ScriptableObject
    {
        [SerializeField] private GuideLineDefinition definition = new GuideLineDefinition();

        /// <summary>Fixed authoring data. Do not modify it during simulation.</summary>
        public GuideLineDefinition Definition => definition;

        public bool TryEvaluate(float distanceM, out GuideLineSample sample)
        {
            return GuideLineCalculator.TryEvaluate(definition, distanceM, out sample);
        }
    }
}
