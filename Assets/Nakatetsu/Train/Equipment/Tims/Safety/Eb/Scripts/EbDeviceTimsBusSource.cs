using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Safety.Eb
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EbDevice))]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class EbDeviceTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey IsEmergencyBrakeRequestedKey =
            new("EB", "IsEmergencyBrakeRequested");
        public static readonly TimsTagKey InactivitySecondsKey =
            new("EB", "InactivitySeconds");
        public static readonly TimsTagKey RemainingSecondsKey =
            new("EB", "RemainingSeconds");

        private EbDevice ebDevice;
        private TrainEquipmentAssignment equipmentAssignment;

        public int AssignedCarIndex => ResolveReferences()
            ? equipmentAssignment.AssignedCarIndex
            : -1;

        private void Awake()
        {
            ResolveReferences();
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            if (localBus == null || !ResolveReferences() || !equipmentAssignment.IsAssigned)
            {
                return;
            }

            if (!isActiveAndEnabled || !ebDevice.isActiveAndEnabled || !ebDevice.HasOutput)
            {
                localBus.Remove(IsEmergencyBrakeRequestedKey);
                localBus.Remove(InactivitySecondsKey);
                localBus.Remove(RemainingSecondsKey);
                return;
            }

            // 時間を進めず、自車の計算済みEB出力をそのまま公開する。
            EbDeviceOutput output = ebDevice.Output;
            localBus.SetBool(IsEmergencyBrakeRequestedKey, output.isEmergencyBrakeRequested);
            localBus.SetFloat(InactivitySecondsKey, output.inactivitySeconds);
            localBus.SetFloat(RemainingSecondsKey, output.remainingSeconds);
        }

        private bool ResolveReferences()
        {
            // 別の車両・編成の装置を参照しないよう、同じGameObject内で解決する。
            if (ebDevice == null)
            {
                ebDevice = GetComponent<EbDevice>();
            }

            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return ebDevice != null && equipmentAssignment != null;
        }
    }
}
