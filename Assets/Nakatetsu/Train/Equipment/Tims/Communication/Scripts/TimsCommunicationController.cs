using UnityEngine;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Speed;

namespace Nakatetsu.Train.Equipment.Tims.Communication
{

    public class TimsCommunicationController : MonoBehaviour, IEquipmentInputSourceCollector
    {
        [SerializeField] private TrainRoot trainRoot;
        private readonly TimsCommunicationContext context = new();
        private TimsSpeedController speedController;

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

            int collectedSourceCount = 0;
            MonoBehaviour[] components = trainRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
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

        public void CollectInputSources()
        {
            // 各車のTIMS送信元を対応するLocalBusへ収集する。
            CollectSources();
            // LocalBusへの全送信が完了してから編成速度を確定する。
            ResolveSpeedController();
            speedController.CalculateAndPublish();
            // 指令処理は任意の同一GameObjectコンポーネントで有効化する。
            if (TryGetComponent(out TimsControlController control) && control.isActiveAndEnabled)
                control.CalculateAndPublish();
        }

        private void Awake()
        {
            ResolveTrainRoot();
            Initialize();
            ResolveSpeedController();
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
