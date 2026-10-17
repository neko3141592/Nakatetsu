using UnityEngine;
using Nakatetsu.Train.Consist;

namespace Nakatetsu.Train
{
    [DisallowMultipleComponent]
    public sealed class TrainRoot : MonoBehaviour
    {
        [SerializeField] private string trainId;
        [SerializeField] private string consistNumber;
        [SerializeField] private string trainNumber;
        [SerializeField] private string operationNumber;
        [SerializeField, Tooltip("初期登録に使う軌道回路IDを手動で設定します。")]
        private string initialCircuitId;
        [SerializeField] private ConsistDefinitionAsset consistDefinition;

        public string TrainId => trainId;
        public string ConsistNumber => consistNumber ?? string.Empty;
        public string TrainNumber => trainNumber ?? string.Empty;
        public string OperationNumber => operationNumber ?? string.Empty;
        public string InitialCircuitId => initialCircuitId ?? string.Empty;
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
