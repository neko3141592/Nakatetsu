using Nakatetsu.Contracts.Communication;
using Nakatetsu.TrafficControl.Server.Communication.Unity;

namespace Nakatetsu.TrafficControl.Server.CTC
{
    public sealed class CTCController
    {
        private readonly object syncRoot = new();
        private readonly CTCContext context = new();
        private readonly UnitySnapshotStore snapshotStore;

        public CTCController(UnitySnapshotStore snapshotStore)
        {
            this.snapshotStore = snapshotStore;
        }

        public Message<UnitySnapshot>? GetSnapshot()
        {
            lock (syncRoot)
            {
                context.Input = snapshotStore.GetLatest();
                CTCLogic.Calculate(context);

                // Context内のデータを外部で変更されないよう、コピーして返す。
                return context.Output is null ? null : UnitySnapshotStore.Copy(context.Output);
            }
        }
    }
}
