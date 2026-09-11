using UnityEngine;
using Nakatetsu.Train.Consist;

namespace Nakatetsu.Train
{
    [DisallowMultipleComponent]
    public sealed class TrainRoot : MonoBehaviour
    {
        [SerializeField] private ConsistDefinitionAsset consistDefinition;

        public ConsistDefinitionAsset ConsistDefinition => consistDefinition;
        public bool HasConsistDefinition => consistDefinition != null;

        public void Configure(ConsistDefinitionAsset definition)
        {
            consistDefinition = definition;
        }
    }
}
