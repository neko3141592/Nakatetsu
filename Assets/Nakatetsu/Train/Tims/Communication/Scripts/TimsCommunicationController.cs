using UnityEngine;
using Nakatetsu.Train.Consist;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Communication
{

    public class TimsCommunicationController : MonoBehaviour
    {
        [SerializeField] private ConsistDefinitionAsset consistDefinition;
        private readonly TimsCommunicationContext context = new();

        public TimsBusState MasterBus => context.State.masterBus;

        public TimsBusState GetLocalBus(int carIndex)
        {
            return context.State.terminals[carIndex].localBus;
        }

        private void Awake()
        {
            Initialize();
        }

        // 初期化
        private void Initialize()
        {
            if (consistDefinition == null)
            {
                Debug.LogWarning("TIMSの編成定義が設定されていません。", this);
                return;
            }

            InitializeTerminal(consistDefinition.CarCount);
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
