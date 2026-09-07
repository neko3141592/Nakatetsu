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

        public int CarCount => cars?.Count ?? 0;
    }
}
