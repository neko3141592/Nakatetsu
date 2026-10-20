using System.Collections.Generic;
using Nakatetsu.Core.Simulation;
using Nakatetsu.Core.Time;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using Nakatetsu.Train.Simulation.Orchestration;
using Unity.Profiling;
using UnityEngine;

namespace Nakatetsu.Application.Simulation
{
    public sealed class ApplicationSimulationController : MonoBehaviour, IWorldTimeSource
    {
        private static readonly ProfilerMarker TickMarker = new("Nakatetsu.WorldTick");
        private static readonly TrackInterlockingManagementOutput EmptyInterlockingOutput =
            new TrackInterlockingManagementContext().Output;

        [SerializeField] private TrainSimulationController[] trains = new TrainSimulationController[0];
        [SerializeField] private TrackCircuitSimulationController trackCircuitSimulation;
        [SerializeField] private TrackConnectionController trackConnections;
        [SerializeField] private TrackAtcController trackAtc;
        [SerializeField] private TrackInterlockingManagementController interlockingManagement;
        [SerializeField, InspectorName("Manual Step Duration Seconds")] private float tickDurationSeconds;
        [SerializeField] private bool isPaused;
        [SerializeField, Min(0.1f)] private float playbackSpeed = 1.0f;

        [SerializeField, Range(0, 23)] private int startHour = 12;
        [SerializeField, Range(0, 59)] private int startMinute;

        private double startTimeSeconds;

        public double WorldTimeSeconds => startTimeSeconds + ElapsedTimeSeconds;

        private double elapsedTimeSeconds;
        private float lastTickDurationSeconds;
        private bool isInitialized;

        public long CompletedTickCount { get; private set; }
        public TrackInterlockingManagementOutput CompletedInterlockingOutput { get; private set; } =
            EmptyInterlockingOutput;
        // 未取得はnull。完了Tickの全回路を保持し、非在線のfalseも含める。
        public IReadOnlyDictionary<string, bool> CompletedOccupiedByCircuitId { get; private set; }
        // 未取得はnull。Connection IDごとの実位置を完了Tickに固定する。
        public IReadOnlyDictionary<string, TrackConnectionState> CompletedTurnoutsById { get; private set; }
        // 全列車分が未取得ならnull。取得済み0本は空のリスト。
        public IReadOnlyList<TrainInitializationInfo> CompletedTrainInitialization { get; private set; }
        public double ElapsedTimeSeconds => elapsedTimeSeconds;
        public float TickDurationSeconds => lastTickDurationSeconds;
        public bool IsPaused => isPaused;
        public float PlaybackSpeed => playbackSpeed;

        public void SetPaused(bool paused) => isPaused = paused;

        private void Awake()
        {
            var registeredTrains = new HashSet<TrainSimulationController>();
            if (tickDurationSeconds <= 0f || float.IsNaN(tickDurationSeconds) || float.IsInfinity(tickDurationSeconds))
            {
                Debug.LogError("正の有限値で手動ステップ幅を設定してください。", this);
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

            if (interlockingManagement == null)
            {
                Debug.LogError("連動管理部を設定してください。", this);
                return;
            }

            foreach (TrainSimulationController train in trains)
            {
                train.SetAutoRefresh(false);
                train.SetTrackCircuitSimulation(trackCircuitSimulation);
            }

            lastTickDurationSeconds = tickDurationSeconds;
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
            if (isPaused || !CanStep())
            {
                return;
            }

            ExecuteTick(Time.unscaledDeltaTime * playbackSpeed);
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
            trackCircuitSimulation.CompleteSourceRegistration();
        }

        [ContextMenu("Step Once")]
        public void StepOnce()
        {
            if (isPaused && CanStep())
            {
                ExecuteTick(tickDurationSeconds);
            }
        }

        private bool CanStep()
        {
            if (!UnityEngine.Application.isPlaying || !isInitialized)
            {
                return false;
            }
            foreach (TrainSimulationController train in trains)
            {
                if (train == null || !train.IsInitialized)
                {
                    return false;
                }
            }
            return true;
        }

        private void ExecuteTick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f || float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds))
            {
                return;
            }

            using var tickScope = TickMarker.Auto();
            // 前tickまでに始まった転換を進め、列車と連動装置が同じ確定位置を使う。
            if (trackConnections != null)
            {
                AdvanceSimulation(trackConnections, deltaTimeSeconds);
            }

            // 初回は列車の初期配置から占有を作る。以後は前tickの確定状態を保持する。
            if (CompletedTickCount == 0 && trackCircuitSimulation != null)
            {
                AdvanceSimulation(trackCircuitSimulation, deltaTimeSeconds);
            }

            foreach (TrainSimulationController train in trains)
            {
                AdvanceSimulation(train, deltaTimeSeconds);
            }

            if (trackCircuitSimulation != null)
            {
                AdvanceSimulation(trackCircuitSimulation, deltaTimeSeconds);
            }

            // 各駅の更新可否は管理部が判断する。
            if (interlockingManagement != null)
            {
                AdvanceSimulation(interlockingManagement, deltaTimeSeconds);
            }

            // 占有と連動の更新後に電文を作り、次tickの車上制御へ渡す。
            if (trackAtc != null && trackAtc.isActiveAndEnabled)
            {
                AdvanceSimulation(trackAtc, deltaTimeSeconds);
            }

            // Tick外の管理部更新とは分け、完了したTickの出力を時刻と一緒に公開する。
            CompletedInterlockingOutput = interlockingManagement != null
                ? interlockingManagement.Context.Output
                : EmptyInterlockingOutput;
            CompletedTurnoutsById = trackConnections != null ? trackConnections.CaptureStates() : null;
            if (trackCircuitSimulation != null)
            {
                var occupied = new Dictionary<string, bool>();
                foreach (var pair in trackCircuitSimulation.Context.State.OccupiedByCircuitId)
                {
                    occupied.Add(pair.Key, pair.Value);
                }
                CompletedOccupiedByCircuitId =
                    new System.Collections.ObjectModel.ReadOnlyDictionary<string, bool>(occupied);
            }
            else
            {
                CompletedOccupiedByCircuitId = null;
            }
            CompletedTrainInitialization = CaptureTrainInitialization();
            elapsedTimeSeconds += deltaTimeSeconds;
            lastTickDurationSeconds = deltaTimeSeconds;
            CompletedTickCount++;
        }

        private IReadOnlyList<TrainInitializationInfo> CaptureTrainInitialization()
        {
            if (CompletedOccupiedByCircuitId == null)
            {
                return null;
            }
            var result = new List<TrainInitializationInfo>();
            foreach (var train in trains)
            {
                var root = train != null ? train.TrainRoot : null;
                if (root == null)
                {
                    return null;
                }

                // 手動設定をそのまま送り、初期登録時の検証はGo側で行う。
                result.Add(new TrainInitializationInfo(root.TrainId ?? string.Empty, root.ConsistNumber,
                    root.TrainNumber, root.OperationNumber, root.InitialCircuitId));
            }

            return result.AsReadOnly();
        }

        private void AdvanceSimulation(ISimulationController controller, float deltaTimeSeconds)
        {
            controller.Calculate(deltaTimeSeconds);
            controller.ApplyOutput(deltaTimeSeconds);
        }
    }
}
