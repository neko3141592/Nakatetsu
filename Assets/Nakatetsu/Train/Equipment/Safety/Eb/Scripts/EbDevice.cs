using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Shared;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class EbDevice : MonoBehaviour, IEquipmentController
    {
        [SerializeField] private MasterController masterController;
        [SerializeField, Min(0f)] private float activationDelaySeconds = 60f;

        private readonly EbDeviceContext context = new();
        private TrainEquipmentAssignment equipmentAssignment;

        public EbDeviceContext Context => context;
        public EbDeviceOutput Output => context.Output;
        public bool IsEmergencyBrakeRequested => context.Output.isEmergencyBrakeRequested;

        private void Awake()
        {
            // 車両割り当てとマスコンの参照を解決する。
            ResolveReferences();
        }

        public void CollectInput()
        {
            // 同じ車両のマスコン状態を値としてContextへコピーする。
            EbDeviceInput input = context.Input;
            input.hasMasterControllerState = ResolveMasterController();
            if (!input.hasMasterControllerState)
            {
                input.masterController = default;
                return;
            }

            input.masterController = new EbMasterControllerInput
            {
                powerPosition = masterController.PowerPosition,
                brakePosition = masterController.BrakePosition,
                reverserPosition = masterController.ReverserPosition,
                isInputEnabled = masterController.IsInputEnabled
            };
        }

        public void Calculate(float deltaTimeSeconds)
        {
            // 収集済みのマスコン状態から無操作時間とEB出力を計算する。
            context.Settings.activationDelaySeconds = Mathf.Max(0f, activationDelaySeconds);
            EbDeviceLogic.Calculate(context, deltaTimeSeconds);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // TIMSへのEB要求出力は接続せず、ContextのOutputだけを公開する。
        }

        public void Configure(MasterController controller, float delaySeconds)
        {
            // 使用するマスコンとEB作動時間を設定する。
            masterController = controller;
            activationDelaySeconds = Mathf.Max(0f, delaySeconds);
        }

        private bool ResolveReferences()
        {
            // 自身の車両割り当てと対応するマスコンを検索する。
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return ResolveMasterController();
        }

        private bool ResolveMasterController()
        {
            // 同じcarIndexが割り当てられたマスコンを編成内から取得する。
            if (masterController != null)
            {
                return true;
            }

            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            if (equipmentAssignment == null || !equipmentAssignment.IsAssigned)
            {
                return false;
            }

            TrainRoot trainRoot = GetComponentInParent<TrainRoot>(true);
            if (trainRoot == null)
            {
                return false;
            }

            foreach (MasterController candidate in
                     trainRoot.GetComponentsInChildren<MasterController>(true))
            {
                if (candidate.AssignedCarIndex == equipmentAssignment.AssignedCarIndex)
                {
                    masterController = candidate;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            // Inspectorの作動時間を0秒以上に補正する。
            activationDelaySeconds = Mathf.Max(0f, activationDelaySeconds);
        }
    }
}
