using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.SpeedMeasurement;
using Nakatetsu.Train.Equipment.Tims.Bus;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Speed
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpeedSensor), typeof(TrainEquipmentAssignment))]
    public sealed class SpeedSensorTimsBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey MeasuredSpeedMpsKey = new("SpeedSensor", "MeasuredSpeedMps");
        private SpeedSensor sensor;
        private TrainEquipmentAssignment assignment;
        private TimsBusState publishedBus;

        public int AssignedCarIndex
        {
            get
            {
                ResolveReferences();
                if (assignment == null || !assignment.IsAssigned)
                {
                    ClearPublishedValue();
                    return -1;
                }
                return assignment.AssignedCarIndex;
            }
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            ResolveReferences();
            if (publishedBus != localBus)
            {
                ClearPublishedValue();
                publishedBus = localBus;
            }

            if (localBus == null || !isActiveAndEnabled || assignment == null ||
                !assignment.IsAssigned || sensor == null ||
                !sensor.TryGetMeasuredSpeedMps(out float speedMps))
            {
                ClearPublishedValue();
                return;
            }

            localBus.SetFloat(MeasuredSpeedMpsKey, speedMps);
        }

        private void ResolveReferences()
        {
            if (sensor == null) sensor = GetComponent<SpeedSensor>();
            if (assignment == null) assignment = GetComponent<TrainEquipmentAssignment>();
        }

        private void ClearPublishedValue()
        {
            publishedBus?.Remove(MeasuredSpeedMpsKey);
        }

        private void OnDisable()
        {
            ClearPublishedValue();
        }
    }
}
