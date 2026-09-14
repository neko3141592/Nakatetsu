using Nakatetsu.Train.Equipment.Tims.Configuration;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims
{
    [DisallowMultipleComponent]
    public sealed class TimsRoot : MonoBehaviour
    {
        [SerializeField] private TimsSettingsAsset settings;

        public TimsSettingsAsset Settings => settings;
        public bool HasSettings => settings != null;

        public void Configure(TimsSettingsAsset value)
        {
            // TIMS全体で使用する設定アセットを割り当てる。
            settings = value;
        }
    }
}
