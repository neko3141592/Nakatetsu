using Nakatetsu.Train.Equipment.Atc;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Signal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/ATC Signal")]
    public sealed class TimsAtcSignal : MonoBehaviour
    {
        private static readonly TimsTagKey SignalKey = new("ATC", "Signal");

        [Header("Tims")]
        [SerializeField] private TimsCommunicationController tims;

        [Header("Objects")]
        [SerializeField] private GameObject redObject;
        [SerializeField] private GameObject greenObject;
        [SerializeField] private GameObject noneObject;

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void OnDisable() => Apply(TrainAtcSignal.None);

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
            Refresh();
        }

        public void Refresh()
        {
            var signal = TrainAtcSignal.None;
            if (isActiveAndEnabled && tims != null && tims.isActiveAndEnabled &&
                tims.MasterBus.TryGetInt(SignalKey, out int value))
            {
                if (value == (int)TrainAtcSignal.Red)
                {
                    signal = TrainAtcSignal.Red;
                }
                else if (value == (int)TrainAtcSignal.Green)
                {
                    signal = TrainAtcSignal.Green;
                }
            }

            // 未接続・未受信・不正値ではNoneへ戻し、前回の現示を残さない。
            Apply(signal);
        }

        private void Apply(TrainAtcSignal signal)
        {
            if (redObject != null) redObject.SetActive(signal == TrainAtcSignal.Red);
            if (greenObject != null) greenObject.SetActive(signal == TrainAtcSignal.Green);
            if (noneObject != null) noneObject.SetActive(signal == TrainAtcSignal.None);
        }
    }
}
