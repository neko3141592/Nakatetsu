using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Indicators
{
    /// <summary>TIMSが報告する編成全車両の戸閉め状態を知らせ灯に表示する。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Train/Presentation/Door Closed Lamp")]
    public sealed class TrainDoorClosedLampPresentation : MonoBehaviour
    {
        [SerializeField] private TimsCommunicationController tims;
        [Tooltip("未指定なら子の Tc1_AnnouncementLamp_Lens を探します。")]
        [SerializeField] private MeshRenderer lampRenderer;
        [SerializeField] private Material onMaterial;
        [SerializeField] private Material offMaterial;

        private void OnEnable()
        {
            if (Application.isPlaying) Apply(false);
        }

        private void LateUpdate()
        {
            if (tims == null)
            {
                TrainRoot owner = GetComponentInParent<TrainRoot>(true);
                if (owner != null)
                    tims = owner.GetComponentInChildren<TimsCommunicationController>(true);
            }

            bool allClosed = tims != null && tims.isActiveAndEnabled
                && tims.MasterBus.TryGetBool(TimsDoorController.HasValidStateKey, out bool valid) && valid
                && tims.MasterBus.TryGetBool(TimsDoorController.AllClosedKey, out bool closed) && closed;
            Apply(allClosed);
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Apply(false);
        }

        private void Apply(bool on)
        {
            if (lampRenderer == null)
            {
                foreach (MeshRenderer candidate in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (candidate.name != "Tc1_AnnouncementLamp_Lens") continue;
                    lampRenderer = candidate;
                    break;
                }
            }

            Material target = on ? onMaterial : offMaterial;
            if (lampRenderer != null && target != null && lampRenderer.sharedMaterial != target)
                lampRenderer.sharedMaterial = target;
        }
    }
}
