using UnityEngine;

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

        public string carId;
        public string displayName;
        public CarType carType;

        public float lengthM;
        public float bogieCenterDistanceM;

        public float emptyMassKg;
        public int passengerCapacity;

        public GameObject tractionEquipmentPrefab;
        public GameObject brakeEquipmentPrefab;
        public GameObject masterControllerPrefab;

    }
}
