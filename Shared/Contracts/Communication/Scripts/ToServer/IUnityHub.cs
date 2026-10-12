using System.Threading.Tasks;
using MagicOnion;

namespace Nakatetsu.Contracts.Communication
{
    // Unityから呼べるサーバーのメソッドを定義する
    public interface IUnityHub
        : IStreamingHub<IUnityHub, IUnityHubReceiver>
    {
        Task UpdateSnapshotAsync(Message<UnitySnapshot> message);
    }
}
