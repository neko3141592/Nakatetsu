using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Safety.Eb;
using UnityEngine;

namespace Nakatetsu.Train.Integration
{
    /// <summary>有効運転台への操作窓口。戻り値は操作を受け付けたかを表す。</summary>
    [DisallowMultipleComponent]
    public sealed class TrainInputController : MonoBehaviour
    {
        private TrainCabResolver resolver;

        internal void Initialize(TrainCabResolver cabResolver)
        {
            resolver = cabResolver;
        }

        public bool MoveNotchTowardBrake()
        {
            if (!TryGetMaster(out MasterController master))
            {
                return false;
            }

            master.MoveOneStepTowardBrake();
            return true;
        }

        public bool MoveNotchTowardPower()
        {
            if (!TryGetMaster(out MasterController master))
            {
                return false;
            }

            master.MoveOneStepTowardPower();
            return true;
        }

        public bool SetNeutral()
        {
            if (!TryGetMaster(out MasterController master))
            {
                return false;
            }

            master.SetNeutral();
            return true;
        }

        public bool MoveReverserForward()
        {
            return MoveReverser(1);
        }

        public bool MoveReverserBackward()
        {
            return MoveReverser(-1);
        }

        public bool ResetEb()
        {
            if (!isActiveAndEnabled || resolver == null ||
                !resolver.TryGetActiveEb(out EbDevice device))
            {
                return false;
            }

            device.RequestReset();
            return true;
        }

        private bool MoveReverser(int step)
        {
            if (!TryGetMaster(out MasterController master))
            {
                return false;
            }

            int position = Mathf.Clamp((int)master.ReverserPosition + step, -1, 1);
            return master.SetReverserPosition((ReverserPosition)position);
        }

        private bool TryGetMaster(out MasterController master)
        {
            master = null;
            if (!isActiveAndEnabled || resolver == null)
            {
                return false;
            }

            return resolver.TryGetActiveMaster(out master, out _) && master.IsInputEnabled;
        }
    }
}
