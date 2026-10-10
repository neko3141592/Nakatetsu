#nullable enable

using System;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Client;
using Nakatetsu.Contracts.Communication;
using UnityEngine;

namespace Nakatetsu.Application.Communication
{
    public sealed class UnityCommunicationController : MonoBehaviour, IUnityHubReceiver
    {
        [SerializeField] private string serverAddress = "http://localhost:5188";

        private GrpcChannel? channel;
        private IUnityHub? hub;

        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public bool IsConnected => hub != null && !hub.WaitForDisconnect().IsCompleted;

        private async void Start()
        {
            var cancellationToken = destroyCancellationToken;
            try
            {
                channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
                {
                    HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                    DisposeHttpClient = true
                });

                var connectedHub = await StreamingHubClient.ConnectAsync<IUnityHub, IUnityHubReceiver>(
                    channel, this, cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    await connectedHub.DisposeAsync();
                    return;
                }

                hub = connectedHub;
                Debug.Log("CTCサーバーに接続しました。", this);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                channel?.Dispose();
                channel = null;
                Debug.LogException(exception, this);
            }
        }

        public IUnityHub GetConnectedHub()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("CTCサーバーへの接続完了後に送信してください。");
            }

            return hub!;
        }

        private async void OnDestroy()
        {
            var connectedHub = hub;
            hub = null;
            try
            {
                if (connectedHub != null)
                {
                    await connectedHub.DisposeAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                channel?.Dispose();
                channel = null;
            }
        }
    }
}
