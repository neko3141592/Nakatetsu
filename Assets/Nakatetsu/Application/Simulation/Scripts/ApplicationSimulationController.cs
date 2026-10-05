using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Interlocking;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Application.Simulation
{
    public sealed class ApplicationSimulationController : MonoBehaviour, IWorldTimeSource
    {
        [SerializeField] private TrainSimulationController[] trains = new TrainSimulationController[0];
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;
        [SerializeField] private TrackAtcController trackAtc;
        [SerializeField] private TrackInterlockingController[] interlockings = new TrackInterlockingController[0];
        [SerializeField] private float tickDurationSeconds;
        [SerializeField] private bool isPaused;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 100;
        [SerializeField, Min(1f)] private float maxSimulationMillisecondsPerFrame = 10f;
        [SerializeField, Min(0.1f)] private float playbackSpeed = 1.0f;

        [SerializeField, Range(0, 23)] private int startHour = 12;
        [SerializeField, Range(0, 59)] private int startMinute;

        private double startTimeSeconds;

        public double WorldTimeSeconds => startTimeSeconds + ElapsedTimeSeconds;

        private float fixedTickSeconds;
        private double pendingTimeSeconds;
        private bool isInitialized;

        public long CompletedTickCount { get; private set; }
        public double ElapsedTimeSeconds => CompletedTickCount * (double)fixedTickSeconds;
        public bool IsPaused => isPaused;
        public float PlaybackSpeed => playbackSpeed;

        public void SetPaused(bool paused) => isPaused = paused;

        private void Awake()
        {
            var registeredTrains = new HashSet<TrainSimulationController>();
            if (tickDurationSeconds <= 0f || float.IsNaN(tickDurationSeconds) || float.IsInfinity(tickDurationSeconds))
            {
                Debug.LogError("正の有限値でtick幅を設定してください。", this);
                return;
            }

            foreach (TrainSimulationController train in trains)
            {
                if (train == null || !registeredTrains.Add(train))
                {
                    Debug.LogError("Trainの未設定または重複を修正してください。", this);
                    return;
                }
            }

            var registeredInterlockings = new HashSet<TrackInterlockingController>();
            foreach (TrackInterlockingController interlocking in interlockings)
            {
                if (interlocking == null || !registeredInterlockings.Add(interlocking))
                {
                    Debug.LogError("Interlockingの未設定または重複を修正してください。", this);
                    return;
                }
            }

            foreach (TrainSimulationController train in trains)
            {
                train.SetAutoRefresh(false);
                train.SetTrackCircuitSimulation(trackCircuitSimulation);
            }

            fixedTickSeconds = tickDurationSeconds;
            if (trackAtc != null)
            {
                trackAtc.SetWorldTimeSource(this);
            }

            // 開始時間を設定
            startTimeSeconds = startHour * 3600d + startMinute * 60d;

            isInitialized = true;
        }

        private void Update()
        {
            if (isPaused || !CanStep()) return;

            pendingTimeSeconds += Time.unscaledDeltaTime * playbackSpeed;
            double frameStartSeconds = Time.realtimeSinceStartupAsDouble;
            double frameBudgetSeconds = Mathf.Max(1f, maxSimulationMillisecondsPerFrame) / 1000d;
            // 処理しきれなかった時間は捨てず、次フレームへ持ち越す。
            for (int i = 0; i < maxTicksPerFrame && pendingTimeSeconds >= fixedTickSeconds; i++)
            {
                ExecuteTick();
                pendingTimeSeconds -= fixedTickSeconds;
                // tickは途中で切らず、実時間の上限に達したら描画と入力へ戻る。
                if (Time.realtimeSinceStartupAsDouble - frameStartSeconds >= frameBudgetSeconds)
                {
                    break;
                }
            }
        }

        private void Start()
        {
            if (!isInitialized || trackCircuitSimulation == null)
            {
                return;
            }

            foreach (TrainSimulationController train in trains)
            {
                if (train.TrackPositionController == null ||
                    !trackCircuitSimulation.RegisterSource(train.TrackPositionController))
                {
                    Debug.LogError("軌道回路への列車占有ソース登録に失敗しました。", this);
                    isInitialized = false;
                    return;
                }
            }
        }

        [ContextMenu("Step Once")]
        public void StepOnce()
        {
            if (isPaused && CanStep()) ExecuteTick();
        }

        private bool CanStep()
        {
            if (!UnityEngine.Application.isPlaying || !isInitialized) return false;
            foreach (TrainSimulationController train in trains)
                if (train == null || !train.IsInitialized) return false;
            foreach (TrackInterlockingController interlocking in interlockings)
                if (interlocking == null || !interlocking.IsInitialized) return false;
            return true;
        }

        private void ExecuteTick()
        {
            // 初回は列車の初期配置から占有を作る。以後は前tickの確定状態を保持する。
            if (CompletedTickCount == 0 && trackCircuitSimulation != null)
                AdvanceSimulation(trackCircuitSimulation);

            foreach (TrainSimulationController train in trains)
                AdvanceSimulation(train);

            if (trackCircuitSimulation != null)
                AdvanceSimulation(trackCircuitSimulation);

            foreach (TrackInterlockingController interlocking in interlockings)
                AdvanceSimulation(interlocking);

            // 占有と連動の更新後に電文を作り、次tickの車上制御へ渡す。
            if (trackAtc != null && trackAtc.isActiveAndEnabled)
                AdvanceSimulation(trackAtc);

            CompletedTickCount++;
        }

        private void AdvanceSimulation(ISimulationController controller)
        {
            controller.Calculate(fixedTickSeconds);
            controller.ApplyOutput(fixedTickSeconds);
        }
    }
}
