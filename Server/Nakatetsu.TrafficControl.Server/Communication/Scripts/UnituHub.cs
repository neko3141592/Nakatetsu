using MagicOnion.Server.Hubs;
using Nakatetsu.Contracts.Communication;
using Nakatetsu.Contracts.World;

namespace Nakatetsu.TrafficControl.Server.Communication
{
    public sealed class UnityHub
        : StreamingHubBase<IUnityHub, IUnityHubReceiver>,
          IUnityHub
    {
        public Task UpdateWorldTimeAsync(
            Message<WorldTimeInformation> message)
        {
            var time = message.Payload;

            Console.WriteLine(
                $"tick: {time.TickCount}, " +
                $"運行時刻: {time.WorldTimeSeconds}, " +
                $"一時停止: {time.IsPaused}");

            return Task.CompletedTask;
        }
    }
}