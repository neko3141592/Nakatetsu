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
            return context.State.terminals[carIndex].localBus;
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
