using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    public sealed class TrainCabInitializer : MonoBehaviour
    {
        [SerializeField] private ActivatedCabPosition initialCab = ActivatedCabPosition.Front;

        internal void Initialize(TrainRoot root)
        {
            if (root == null || root.ConsistDefinition == null || root.ConsistDefinition.CarCount < 2 ||
                (initialCab != ActivatedCabPosition.Front && initialCab != ActivatedCabPosition.Rear))
            {
                Debug.LogError("運転台初期化には前後の運転台を持つ編成と有効な初期運転台が必要です。", this);
                return;
            }

            int rearIndex = root.ConsistDefinition.CarCount - 1;
            MasterController frontMaster = null;
            MasterController rearMaster = null;
            CabActivationSwitchController frontSwitch = null;
            CabActivationSwitchController rearSwitch = null;

            foreach (MasterController master in root.GetComponentsInChildren<MasterController>(true))
            {
                if (master.GetComponentInParent<TrainRoot>(true) != root)
                {
                    continue;
                }

                if (master.AssignedCarIndex == 0)
                {
                    frontMaster = master;
                }

                if (master.AssignedCarIndex == rearIndex)
                {
                    rearMaster = master;
                }
            }

            foreach (CabActivationSwitchController cab in root.GetComponentsInChildren<CabActivationSwitchController>(true))
            {
                if (cab.GetComponentInParent<TrainRoot>(true) != root)
                {
                    continue;
                }

                var assignment = cab.GetComponent<TrainEquipmentAssignment>();
                if (assignment == null)
                {
                    continue;
                }

                if (assignment.AssignedCarIndex == 0)
                {
                    frontSwitch = cab;
                }

                if (assignment.AssignedCarIndex == rearIndex)
                {
                    rearSwitch = cab;
                }
            }

            if (frontMaster == null || rearMaster == null || frontSwitch == null || rearSwitch == null)
            {
                Debug.LogError("前後のマスコンと運転台スイッチを生成してから運転台を初期化してください。", this);
                return;
            }

            frontMaster.SetInputEnabled(true);
            rearMaster.SetInputEnabled(true);
            frontMaster.SetEmergencyBrake();
            rearMaster.SetEmergencyBrake();
            frontMaster.SetReverserPosition(ReverserPosition.Neutral);
            rearMaster.SetReverserPosition(ReverserPosition.Neutral);
            bool rear = initialCab == ActivatedCabPosition.Rear;
            frontSwitch.SetPosition(rear ? CabActivationPosition.Rear : CabActivationPosition.Front);
            rearSwitch.SetPosition(rear ? CabActivationPosition.Front : CabActivationPosition.Rear);
        }
    }
}
