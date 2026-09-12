using UnityEngine;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Communication
{

    public class TimsCommunicationController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        private readonly TimsCommunicationContext context = new();

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

        private void Awake()
        {
            ResolveTrainRoot();
            Initialize();
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
