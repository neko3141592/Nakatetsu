using Nakatetsu.Train.Equipment.Tims.Operation;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainRoot))]
    public sealed class TrainStatusProvider : MonoBehaviour
    {
        private TimsDirectionController directionController;

        /// <summary>
        /// TIMSが最後に判定した有効運転台を取得する。
        /// falseは取得元が利用できない状態、trueかつNoneは有効運転台なしを表す。
        /// </summary>
        public bool TryGetActiveCab(out ActivatedCabPosition position)
        {
            position = ActivatedCabPosition.None;
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (directionController == null)
            {
                directionController = GetComponentInChildren<TimsDirectionController>(true);
            }

            if (directionController == null || !directionController.isActiveAndEnabled)
            {
                return false;
            }

            position = directionController.Output.activatedCabPosition;
            return true;
        }
    }
}
