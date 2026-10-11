using System.Threading.Tasks;

namespace Nakatetsu.Application.Communication
{
    public interface IServerMessageSender
    {
        Task SendAsync();
    }
}
