using UnityEngine;
using Nakatetsu.Train.Equipment.Traction.Vvvf;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Traction;

namespace Nakatetsu.Train.Equipment.Tims.Traction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainEquipmentAssignment))]
    public sealed class TimsTractionBusSource : MonoBehaviour, ITimsBusSource
    {
        public static readonly TimsTagKey IsAvailableKey =
            new("Traction", "IsAvailable");
        public static readonly TimsTagKey MotorCountKey =
            new("Traction", "MotorCount");
        public static readonly TimsTagKey RatedPowerWKey =
            new("Traction", "RatedPowerW");
        public static readonly TimsTagKey ActualTractionForceNKey =
            new("Traction", "ActualTractionForceN");

        public static readonly TimsTagKey IsVvvfMotorCarKey = new("Traction", "IsVvvfMotorCar");
        public static readonly TimsTagKey RegenCapacityNKey = new("Traction", "RegenCapacityN");
        public static readonly TimsTagKey ActualRegenForceNKey = new("Traction", "ActualRegenForceN");

        // VVVF 1台が受け持つモーターの実効電流合計。正が力行、負が回生。
        public static readonly TimsTagKey SignedMotorCurrentAKey = new("Traction", "SignedMotorCurrentA");

        [SerializeField] private MonoBehaviour tractionEquipmentComponent;
        [SerializeField] private TrainEquipmentAssignment equipmentAssignment;

        private ITractionEquipment tractionEquipment;

        public int AssignedCarIndex => ResolveReferences()
            ? equipmentAssignment.AssignedCarIndex
            : -1;
        public ITractionEquipment TractionEquipment => ResolveTractionEquipment()
            ? tractionEquipment
            : null;

        private void Awake()
        {
            ResolveReferences();
        }

        public void WriteTimsBus(TimsBusState localBus)
        {
            if (localBus == null) return;
            localBus.Remove(SignedMotorCurrentAKey);
            if (!ResolveReferences())
            {
                return;
            }

            VvvfController vvvf = tractionEquipment as VvvfController;
            if (isActiveAndEnabled && vvvf != null && vvvf.isActiveAndEnabled && vvvf.IsAvailable)
            {
                float currentA = vvvf.TotalMotorCurrentRmsA;
                float forceN = vvvf.ActualTractionForceN;
                if (!float.IsNaN(currentA) && !float.IsInfinity(currentA) && currentA >= 0f &&
                    !float.IsNaN(forceN) && !float.IsInfinity(forceN))
                {
                    // 測定力は進行方向を適用する前の値。後退力行も正、回生は負になる。
                    // 指令モードではなく、前ステップの実測値同士を組み合わせる。
                    localBus.SetFloat(SignedMotorCurrentAKey, forceN < 0f ? -currentA : currentA);
                }
            }
            localBus.SetBool(IsVvvfMotorCarKey, vvvf != null && vvvf.MotorCount > 0);
            localBus.SetFloat(RegenCapacityNKey, vvvf != null ? vvvf.RegenCapacityN : 0f);
            // 実回生力は前ステップのモーター測定値。指令値で代用しない。
            localBus.SetFloat(ActualRegenForceNKey, vvvf != null ? vvvf.ActualRegenForceN : 0f);
            localBus.SetBool(IsAvailableKey, tractionEquipment.IsAvailable && tractionEquipmentComponent.isActiveAndEnabled);
            localBus.SetInt(MotorCountKey, tractionEquipment.MotorCount);
            localBus.SetFloat(RatedPowerWKey, tractionEquipment.RatedPowerW);
            localBus.SetFloat(
                ActualTractionForceNKey,
                tractionEquipment.ActualTractionForceN);
        }

        private bool ResolveReferences()
        {
            if (equipmentAssignment == null)
            {
                equipmentAssignment = GetComponent<TrainEquipmentAssignment>();
            }

            return equipmentAssignment != null &&
                   equipmentAssignment.IsAssigned &&
                   ResolveTractionEquipment();
        }

        private bool ResolveTractionEquipment()
        {
            if (tractionEquipmentComponent != null &&
                tractionEquipmentComponent.gameObject == gameObject &&
                tractionEquipmentComponent is ITractionEquipment assignedEquipment)
            {
                tractionEquipment = assignedEquipment;
                return true;
            }

            tractionEquipmentComponent = null;
            tractionEquipment = null;

            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component is not ITractionEquipment equipment)
                {
                    continue;
                }

                tractionEquipmentComponent = component;
                tractionEquipment = equipment;
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
