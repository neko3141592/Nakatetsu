using UnityEngine;
using Nakatetsu.Train.Drive;

namespace Nakatetsu.Train.Consist
{
    public enum CarType
    {
        T,
        Tc1,
        Tc2,
        M,
        Mc1,
        Mc2
    }

    [CreateAssetMenu(fileName = "CarDefinition", menuName = "Nakatetsu/Train/Car Definition")]
    public class CarDefinitionAsset : ScriptableObject
    {

        [Header("Identification")]
        public string carId;
        public string displayName;
        public CarType carType;


        [Header("Vehicle")]
        public float lengthM;
        public float bogieCenterDistanceM;

        public float emptyMassKg;
        public int passengerCapacity;

        public TrainDriveDefinition driveDefinition;


        [Header("Equipment")]
        public GameObject tractionEquipmentPrefab;
        public GameObject brakeEquipmentPrefab;

        [Header("Cab Equipment")]
        public GameObject masterControllerPrefab;
        public GameObject cabActivationSwitchPrefab;

    }
}
