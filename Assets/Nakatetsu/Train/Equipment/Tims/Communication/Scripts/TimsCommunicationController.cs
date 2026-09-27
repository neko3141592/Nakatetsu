using UnityEngine;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Speed;
using Nakatetsu.Train.Equipment.Tims.Traction;

namespace Nakatetsu.Train.Equipment.Tims.Communication
{

    public class TimsCommunicationController : MonoBehaviour, IEquipmentInputSourceCollector
    {
        [SerializeField] private TrainRoot trainRoot;
        [SerializeField, Min(0f)] private float transferIntervalSeconds = 0.25f;
        private readonly TimsCommunicationContext context = new();
        private TimsSpeedController speedController;
        private TimsCurrentController currentController;

        public TimsBusState MasterBus => context.State.masterBus;

        public TimsBusState GetLocalBus(int carIndex)
        {
            TryGetLocalBus(carIndex, out TimsBusState localBus);
            return localBus;
        }

        public bool TryGetLocalBus(int carIndex, out TimsBusState localBus)
        {
            foreach (TimsCarTerminalState terminal in context.State.terminals)
            {
                if (terminal.carIndex != carIndex)
                {
                    continue;
                }

                localBus = terminal.localBus;
                return localBus != null;
            }

            localBus = null;
            return false;
        }

        public int CollectSources()
        {
            if (!ResolveTrainRoot())
            {
                return 0;
            }

            // 電流は今回受信できた装置だけを代表候補にする。撤去・割当解除時の古い値を残さない。
            foreach (TimsCarTerminalState terminal in context.State.terminals)
                terminal.localBus.Remove(TimsTractionBusSource.SignedMotorCurrentAKey);

            int collectedSourceCount = 0;
            MonoBehaviour[] components = trainRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                // 割当やLocalBusが欠けたドアも搭載として認識し、監視を有効にする。
                if (component is DoorTimsBusSource && !TryGetComponent<TimsDoorController>(out _))
                {
                    gameObject.AddComponent<TimsDoorController>();
                }
                if (component is not ITimsBusSource source ||
                    !TryGetLocalBus(source.AssignedCarIndex, out TimsBusState localBus))
                {
                    continue;
                }

                source.WriteTimsBus(localBus);
                collectedSourceCount++;
            }

            return collectedSourceCount;
        }

        public void CollectInputSources(float deltaTimeSeconds)
        {
            if (TimsCommunicationLogic.ShouldCollectSources(context.State, deltaTimeSeconds, transferIntervalSeconds))
            {
                CollectSources();
            }

            CalculateAndPublish();
        }

        /// <summary>テスト・手動収集用。転送間隔を待たずに現在値を収集する。</summary>
        public void CollectInputSources()
        {
            CollectSources();
            context.State.hasCollectedSources = true;
            context.State.collectionElapsedSeconds = 0d;
            CalculateAndPublish();
        }

        private void CalculateAndPublish()
        {
            // 転送待ちのtickでも、受信済みのLocalBusを使って計算を続ける。
            ResolveSpeedController();
            speedController.CalculateAndPublish();
            ResolveCurrentController();
            currentController.CalculateAndPublish();
            // 前ステップのドア接点を集約。1両でも未取得なら全扉閉にはしない。
            if (TryGetComponent(out TimsDoorController doors))
            {
                doors.CalculateAndPublish();
            }
            // 指令処理は任意の同一GameObjectコンポーネントで有効化する。
            if (TryGetComponent(out TimsControlController control) && control.isActiveAndEnabled)
            {
                control.CalculateAndPublish();
            }
        }

        private void Awake()
        {
            ResolveTrainRoot();
            Initialize();
            ResolveSpeedController();
            ResolveCurrentController();
        }

        private void ResolveSpeedController()
        {
            if (speedController == null)
            {
                speedController = GetComponent<TimsSpeedController>();
                if (speedController == null)
                {
                    speedController = gameObject.AddComponent<TimsSpeedController>();
                }
            }
        }

        private void ResolveCurrentController()
        {
            if (currentController == null)
            {
                currentController = GetComponent<TimsCurrentController>();
                if (currentController == null)
                    currentController = gameObject.AddComponent<TimsCurrentController>();
            }
        }

        public void Configure(TrainRoot owner)
        {
            trainRoot = owner;
        }

        // 初期化
        private void Initialize()
        {
            var consistDefinition = trainRoot != null
                ? trainRoot.ConsistDefinition
                : null;
            if (consistDefinition == null)
            {
                Debug.LogWarning("TIMSの編成定義が設定されていません。", this);
                return;
            }

            InitializeTerminal(consistDefinition.CarCount);
        }

        private bool ResolveTrainRoot()
        {
            if (trainRoot == null)
            {
                trainRoot = GetComponentInParent<TrainRoot>(true);
            }

            return trainRoot != null;
        }

        private void InitializeTerminal(int carCount)
        {
            context.State.terminals.Clear();

            for (int i = 0; i < carCount; i++)
            {
                context.State.terminals.Add(
                    new TimsCarTerminalState
                    {
                        carIndex = i
                    }
                );
            }
        }

    }
}
