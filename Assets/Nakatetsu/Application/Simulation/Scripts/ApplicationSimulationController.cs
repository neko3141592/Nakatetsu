using System.Collections.Generic;
using Nakatetsu.Core.Time;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Application.Simulation
{
    public sealed class ApplicationSimulationController : MonoBehaviour, IWorldTimeSource
    {
        [SerializeField] private TrainSimulationController[] trains = new TrainSimulationController[0];
        [SerializeField] private float tickDurationSeconds;
        [SerializeField] private bool isPaused;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 100;
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

            foreach (TrainSimulationController train in trains)
                train.SetAutoRefresh(false);

            fixedTickSeconds = tickDurationSeconds;

            // 開始時間を設定
            startTimeSeconds = startHour * 3600d + startMinute * 60d;

            isInitialized = true;
        }

        private void Update()
        {
            if (isPaused || !CanStep()) return;

            pendingTimeSeconds += Time.unscaledDeltaTime * playbackSpeed;
            // 処理しきれなかった時間は捨てず、次フレームへ持ち越す。
            for (int i = 0; i < maxTicksPerFrame && pendingTimeSeconds >= fixedTickSeconds; i++)
            {
                ExecuteTick();
                pendingTimeSeconds -= fixedTickSeconds;
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
            return true;
        }

        private void ExecuteTick()
        {
            foreach (TrainSimulationController train in trains)
                train.Step(fixedTickSeconds);

            CompletedTickCount++;
        }
    }
}
