using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Presentation.Shared;
using UnityEngine;

namespace Nakatetsu.Train.Presentation.Audio
{
    /// <summary>同じ車両のEB出力を音にする。再生許可はApplicationの注目先制御から受け取る。</summary>
    [DisallowMultipleComponent]
    public sealed class CabAudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource ebBuzzer;

        private static readonly List<CabAudioController> activeControllers = new();
        private static readonly IReadOnlyList<CabAudioController> activeView = activeControllers.AsReadOnly();
        private TrainRoot owner;
        private TrainPresentationAssignment presentationAssignment;
        private TrainEquipmentAssignment equipmentAssignment;
        private EbDevice ebDevice;
        private bool isAudible;

        // OnEnable/OnDisableで管理し、注目先制御による毎フレームのシーン探索を避ける。
        public static IReadOnlyList<CabAudioController> ActiveControllers => activeView;
        public TrainRoot Owner => owner;
        public int AssignedCarIndex => presentationAssignment != null ? presentationAssignment.AssignedCarIndex : -1;
        public EbDevice EbDevice => ebDevice;
        public bool IsAudible => isAudible;
        public bool IsBuzzerPlaying => ebBuzzer != null && ebBuzzer.isPlaying;

        private void Awake()
        {
            if (ebBuzzer != null)
            {
                ebBuzzer.playOnAwake = false;
                ebBuzzer.loop = true;
                ebBuzzer.spatialBlend = 0f;
                ebBuzzer.Stop();
            }
        }

        private void OnEnable()
        {
            if (!activeControllers.Contains(this)) activeControllers.Add(this);
        }

        private void Start()
        {
            // PresentationBuilderの車両Index割り当て後に、機器を一度だけ解決する。
            if (!BindEbFromAssignment())
                Debug.LogWarning("CabAudio: 同じ編成・車両IndexのEB装置を1つ配置してください。", this);
        }

        /// <summary>初期生成後、または機器の再生成後に呼ぶ接続処理。</summary>
        public bool BindEbFromAssignment()
        {
            StopBuzzer();
            ebDevice = null;
            equipmentAssignment = null;
            owner = GetComponentInParent<TrainRoot>(true);
            presentationAssignment = GetComponentInParent<TrainPresentationAssignment>(true);
            if (owner == null || presentationAssignment == null || !presentationAssignment.IsAssigned ||
                presentationAssignment.GetComponentInParent<TrainRoot>(true) != owner)
                return false;

            EbDevice match = null;
            foreach (EbDevice candidate in owner.GetComponentsInChildren<EbDevice>(true))
            {
                if (candidate.GetComponentInParent<TrainRoot>(true) != owner ||
                    !candidate.TryGetComponent(out TrainEquipmentAssignment assignment) ||
                    assignment.AssignedCarIndex != AssignedCarIndex)
                    continue;

                // 同じ車両に複数ある場合、任意の機器を選んで鳴らさない。
                if (match != null) return false;
                match = candidate;
            }

            ebDevice = match;
            if (ebDevice != null) equipmentAssignment = ebDevice.GetComponent<TrainEquipmentAssignment>();
            return ebDevice != null;
        }

        public void SetAudible(bool audible)
        {
            isAudible = audible && isActiveAndEnabled;
            RefreshAudio();
        }

        private void LateUpdate()
        {
            RefreshAudio();
        }

        private void RefreshAudio()
        {
            bool shouldPlay = isAudible && owner != null && owner.isActiveAndEnabled &&
                AssignedCarIndex >= 0 && equipmentAssignment != null &&
                equipmentAssignment.AssignedCarIndex == AssignedCarIndex &&
                ebDevice != null && ebDevice.isActiveAndEnabled && ebDevice.HasOutput && ebDevice.IsBuzzerRequested;
            if (!shouldPlay)
            {
                StopBuzzer();
                return;
            }

            // 連続した鳴動中は再スタートせず、同じループを維持する。
            if (ebBuzzer != null && ebBuzzer.isActiveAndEnabled && ebBuzzer.clip != null && !ebBuzzer.isPlaying)
                ebBuzzer.Play();
        }

        private void StopBuzzer()
        {
            if (ebBuzzer != null && ebBuzzer.isPlaying) ebBuzzer.Stop();
        }

        private void OnDisable()
        {
            isAudible = false;
            StopBuzzer();
            activeControllers.Remove(this);
        }
    }
}
