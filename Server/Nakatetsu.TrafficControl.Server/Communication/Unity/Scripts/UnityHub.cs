using MagicOnion.Server.Hubs;
using Nakatetsu.Contracts.Communication;

namespace Nakatetsu.TrafficControl.Server.Communication.Unity
{
    public sealed class UnityHub
        : StreamingHubBase<IUnityHub, IUnityHubReceiver>,
          IUnityHub
    {

        private readonly UnitySnapshotStore snapshotStore;

        public UnityHub(UnitySnapshotStore snapshotStore)
        {
            this.snapshotStore = snapshotStore;
        }

        public Task UpdateSnapshotAsync(Message<UnitySnapshot> message)
        {
            snapshotStore.Update(message);
            var snapshot = message.Payload;

            Console.WriteLine(
                $"tick: {snapshot.WorldTime.TickCount}, " +
                $"運行時刻: {snapshot.WorldTime.WorldTimeSeconds}, " +
                $"一時停止: {snapshot.WorldTime.IsPaused}, " +
                $"連動状態: {snapshot.Interlocking.StationsById.Count}駅");

            return Task.CompletedTask;
        }
    }
}
