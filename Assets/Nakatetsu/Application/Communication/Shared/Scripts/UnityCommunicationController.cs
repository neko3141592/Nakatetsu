#nullable enable

using System;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Client;
using Nakatetsu.Contracts.Communication;
using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;

namespace Nakatetsu.Application.Communication
{
    public sealed class UnityCommunicationController : MonoBehaviour, IUnityHubReceiver
    {
        [SerializeField] private string serverAddress = "http://localhost:5188";
        [SerializeField, Min(0.1f)] private float retryIntervalSeconds = 3f;

        private GrpcChannel? channel;
        private IUnityHub? hub;

        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public bool IsConnected => hub != null && !hub.WaitForDisconnect().IsCompleted;


        private async void Start()
        {
            var cancellationToken = destroyCancellationToken;

            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        channel = GrpcChannel.ForAddress(
                            serverAddress,
                            new GrpcChannelOptions
                            {
                                HttpHandler = new YetAnotherHttpHandler
                                {
                                    Http2Only = true
                                },
                                DisposeHttpClient = true
                            }
                        );

                        hub = await StreamingHubClient.ConnectAsync<IUnityHub, IUnityHubReceiver>(
                            channel, 
                            this, 
                            option: new CallOptions(cancellationToken: cancellationToken),
                            cancellationToken: cancellationToken
                        );

                        // キャンセルされていればここで処理を止める
                        cancellationToken.ThrowIfCancellationRequested();

                        Debug.Log("CTCサーバーに接続しました。", this);

                        // 接続中はここで待つ。切断されると先へ進む。
                        await hub.WaitForDisconnect();
                    }
                    catch (Exception exception)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Debug.LogWarning(
                                $"CTC接続を再試行します: {exception.Message}",
                                this);
                        }
                    }
                    finally
                    {
                        await DisconnectAsync();
                    }

                    // 指定した時間後に再接続
                    await Task.Delay(
                        TimeSpan.FromSeconds(Mathf.Max(0.1f, retryIntervalSeconds)),
                        cancellationToken
                    );
                }
            } 
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // オブジェクトが破棄されたので終了する。
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

        private async Task DisconnectAsync()
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
