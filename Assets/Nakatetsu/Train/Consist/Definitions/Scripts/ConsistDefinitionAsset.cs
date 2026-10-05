using UnityEngine;
using System.Collections.Generic;

namespace Nakatetsu.Train.Consist
{
    [CreateAssetMenu(fileName = "ConsistDefinition", menuName = "Nakatetsu/Train/Consist Definition")]
    public class ConsistDefinitionAsset : ScriptableObject
    {

        public string consistId;
        public string displayName;

        public List<CarDefinitionAsset> cars = new();

        [Header("Common Equipment")]
        [Tooltip("Commonに配置する編成共通機器。同名の機器がある場合は再利用します。")]
        public GameObject[] commonEquipmentPrefabs = new GameObject[0];

        public int CarCount => cars?.Count ?? 0;
    }
}
