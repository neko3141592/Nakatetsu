using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    internal sealed class TrainCabResolver
    {
        private readonly TrainRoot trainRoot;
        private readonly List<MasterController> masterControllers = new();
        private readonly List<EbDevice> ebDevices = new();
        private TimsDirectionController directionController;

        public TrainCabResolver(TrainRoot trainRoot)
        {
            this.trainRoot = trainRoot;
        }

        /// <summary>
        /// TIMSが最後に判定した有効運転台を取得する。
        /// falseは取得元が利用できない状態、trueかつNoneは有効運転台なしを表す。
        /// </summary>
        public bool TryGetActiveCab(out ActivatedCabPosition position)
        {
            position = ActivatedCabPosition.None;
            if (trainRoot == null || !trainRoot.isActiveAndEnabled)
            {
                return false;
            }

            if (directionController == null)
            {
                directionController = trainRoot.GetComponentInChildren<TimsDirectionController>(true);
            }

            if (directionController == null || !directionController.isActiveAndEnabled)
            {
                return false;
            }

            position = directionController.Output.activatedCabPosition;
            return true;
        }

        internal bool TryGetActiveEb(out EbDevice activeDevice)
        {
            activeDevice = null;
            if (!TryGetActiveCab(out ActivatedCabPosition cabPosition) ||
                (cabPosition != ActivatedCabPosition.Front && cabPosition != ActivatedCabPosition.Rear) ||
                trainRoot.ConsistDefinition == null || trainRoot.ConsistDefinition.CarCount <= 0)
            {
                return false;
            }

            int carIndex = cabPosition == ActivatedCabPosition.Front
                ? 0
                : trainRoot.ConsistDefinition.CarCount - 1;

            trainRoot.GetComponentsInChildren<EbDevice>(true, ebDevices);
            foreach (EbDevice device in ebDevices)
            {
                if (!device.isActiveAndEnabled ||
                    device.GetComponentInParent<TrainRoot>(true) != trainRoot ||
                    !device.TryGetComponent(out TrainEquipmentAssignment assignment) ||
                    assignment.AssignedCarIndex != carIndex)
                {
                    continue;
                }

                if (activeDevice != null)
                {
                    activeDevice = null;
                    return false;
                }

                activeDevice = device;
            }

            return activeDevice != null;
        }

        internal bool TryGetActiveMaster(out MasterController activeMaster, out ActivatedCabPosition cabPosition)
        {
            activeMaster = null;
            if (!TryGetActiveCab(out cabPosition) ||
                (cabPosition != ActivatedCabPosition.Front && cabPosition != ActivatedCabPosition.Rear))
            {
                return false;
            }

            if (trainRoot == null || trainRoot.ConsistDefinition == null ||
                trainRoot.ConsistDefinition.CarCount <= 0)
            {
                return false;
            }

            int carIndex = cabPosition == ActivatedCabPosition.Front
                ? 0
                : trainRoot.ConsistDefinition.CarCount - 1;

            // Builderによる生成・再生成後も現在の機器を取得する。Listは再利用する。
            trainRoot.GetComponentsInChildren<MasterController>(true, masterControllers);
            foreach (MasterController master in masterControllers)
            {
                if (!master.isActiveAndEnabled || master.AssignedCarIndex != carIndex ||
                    master.GetComponentInParent<TrainRoot>(true) != trainRoot)
                {
                    continue;
                }

                // 同じ車両に候補が複数ある場合は、任意の機器を選ばない。
                if (activeMaster != null)
                {
                    activeMaster = null;
                    return false;
                }

                activeMaster = master;
            }

            return activeMaster != null;
        }
    }
}
