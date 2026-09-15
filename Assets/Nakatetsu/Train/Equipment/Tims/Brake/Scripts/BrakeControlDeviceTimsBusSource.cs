using Nakatetsu.Train.Equipment.Brake.ControlDevice;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Brake
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BrakeControlDevice), typeof(TrainEquipmentAssignment))]
    public sealed class BrakeControlDeviceTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey MassKgKey = new("Brake", "MeasuredMassKg");
        public static readonly TimsTagKey PressureKPaKey = new("Brake", "MeasuredBCPressureKPa");
        public static readonly TimsTagKey ActualForceNKey = new("Brake", "ActualForceN");
        public static readonly TimsTagKey ForcePerKPaKey = new("Brake", "ForcePerKPa");
        public static readonly TimsTagKey MaximumPressureKPaKey = new("Brake", "MaximumPressureKPa");
        public int AssignedCarIndex => GetComponent<TrainEquipmentAssignment>().AssignedCarIndex;

        public void WriteTimsBus(TimsBusState bus)
        {
            var device = GetComponent<BrakeControlDevice>();
            if (!isActiveAndEnabled || !device.isActiveAndEnabled || device.MeasuredMassKg <= 0f)
            {
                bus.Remove(MassKgKey);
                bus.Remove(PressureKPaKey);
                bus.Remove(ActualForceNKey);
                bus.Remove(ForcePerKPaKey);
                bus.Remove(MaximumPressureKPaKey);
                return;
            }
            bus.SetFloat(MassKgKey, device.MeasuredMassKg);
            bus.SetFloat(PressureKPaKey, device.MeasuredPressureKPa);
            bus.SetFloat(ActualForceNKey, device.ActualBrakeForceN);
            bus.SetFloat(ForcePerKPaKey, device.ForcePerKPa);
            bus.SetFloat(MaximumPressureKPaKey, device.MaximumPressureKPa);
        }
    }
}
