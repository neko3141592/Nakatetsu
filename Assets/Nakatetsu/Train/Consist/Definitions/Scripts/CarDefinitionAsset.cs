using UnityEngine;
using Nakatetsu.Train.Equipment.Traction.Drive;
using Nakatetsu.Train.Simulation.Brake;
using Nakatetsu.Train.Simulation.Traction.Motor;

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
        public GameObject[] additionalEquipmentPrefabs;

        [Header("Physical Simulation")]
        public MotorDefinitionAsset motorDefinition;
        [Min(0)] public int motorCount;
        public BrakeCylinderDefinitionAsset brakeCylinderDefinition;
        [Min(0)] public int brakeCylinderCount = 4;
        public GameObject motorSimulationPrefab;
        public GameObject brakeSimulationPrefab;
        public GameObject loadSimulationPrefab;

        [Header("Cab Equipment")]
        public GameObject masterControllerPrefab;
        public GameObject cabActivationSwitchPrefab;

    }
}
