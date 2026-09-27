using Nakatetsu.Application.Player;
using Nakatetsu.Train;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Integration;
using Nakatetsu.Train.Presentation.Audio;
using UnityEngine;

namespace Nakatetsu.Application.Audio
{
    /// <summary>プレイヤーの注目先に基づき、各運転台の音声再生を許可する。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class ApplicationAudioController : MonoBehaviour
    {
        [SerializeField] private TrainFocusController focusController;

        private void LateUpdate()
        {
            RefreshCabAudio();
        }

        /// <summary>注目編成の有効運転台にある音源だけに再生を許可する。</summary>
        public void RefreshCabAudio()
        {
            TrainRoot owner = null;
            int carIndex = -1;
            TrainIntegrationController train = focusController != null && focusController.isActiveAndEnabled
                ? focusController.FocusedTrain
                : null;
            if (isActiveAndEnabled && train != null && train.isActiveAndEnabled &&
                train.Status != null && train.Status.TryGetActiveCab(out ActivatedCabPosition cab))
            {
                owner = train.GetComponentInParent<TrainRoot>(true);
                if (owner != null && owner.isActiveAndEnabled && owner.ConsistDefinition != null &&
                    owner.ConsistDefinition.CarCount > 0)
                {
                    if (cab == ActivatedCabPosition.Front) carIndex = 0;
                    else if (cab == ActivatedCabPosition.Rear) carIndex = owner.ConsistDefinition.CarCount - 1;
                }
            }

            CabAudioController selected = null;
            bool ambiguous = false;
            foreach (CabAudioController audio in CabAudioController.ActiveControllers)
            {
                if (carIndex < 0 || audio.Owner != owner || audio.AssignedCarIndex != carIndex) continue;
                if (selected != null) ambiguous = true;
                selected = audio;
            }

            foreach (CabAudioController audio in CabAudioController.ActiveControllers)
                audio.SetAudible(!ambiguous && audio == selected);
        }

        private void OnDisable()
        {
            foreach (CabAudioController audio in CabAudioController.ActiveControllers)
                audio.SetAudible(false);
        }
    }
}
