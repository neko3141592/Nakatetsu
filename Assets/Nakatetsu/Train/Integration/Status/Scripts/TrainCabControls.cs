using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Tims.Operation;

namespace Nakatetsu.Train.Integration
{
    /// <summary>有効運転台の操作位置を読み取った時点の値。</summary>
    public readonly struct TrainCabControls
    {
        public ActivatedCabPosition CabPosition { get; }
        public int PowerPosition { get; }
        /// <summary>非常位置を含む制動ノッチ。</summary>
        public int BrakePosition { get; }
        /// <summary>マスコンが非常位置かどうか。TIMSが集約した非常要求とは別。</summary>
        public bool IsEmergencyBrake { get; }
        public ReverserPosition ReverserPosition { get; }

        public TrainCabControls(
            ActivatedCabPosition cabPosition,
            int powerPosition,
            int brakePosition,
            bool isEmergencyBrake,
            ReverserPosition reverserPosition)
        {
            CabPosition = cabPosition;
            PowerPosition = powerPosition;
            BrakePosition = brakePosition;
            IsEmergencyBrake = isEmergencyBrake;
            ReverserPosition = reverserPosition;
        }
    }
}
