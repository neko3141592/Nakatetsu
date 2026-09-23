using UnityEngine;
using Nakatetsu.Train.Consist;

namespace Nakatetsu.Train
{
    [DisallowMultipleComponent]
    public sealed class TrainRoot : MonoBehaviour
    {
        [SerializeField] private string trainId;
        [SerializeField] private ConsistDefinitionAsset consistDefinition;

        public string TrainId => trainId;
        public ConsistDefinitionAsset ConsistDefinition => consistDefinition;
        public bool HasConsistDefinition => consistDefinition != null;

        public void SetTrainId(string id)
        {
            trainId = id;
        }

        public void Configure(ConsistDefinitionAsset definition)
        {
            consistDefinition = definition;
        }
    }
}
