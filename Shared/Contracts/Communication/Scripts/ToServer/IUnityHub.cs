using System.Threading.Tasks;
using MagicOnion;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Contracts.World;

namespace Nakatetsu.Contracts.Communication
{
    // Unityから呼べるサーバーのメソッドを定義する
    public interface IUnityHub
        : IStreamingHub<IUnityHub, IUnityHubReceiver>
    {
        Task UpdateWorldTimeAsync(
            Message<WorldTimeInformation> message);

        Task UpdateInterlockingSnapshotAsync(
            Message<InterlockingSnapshot> message);
    }
}