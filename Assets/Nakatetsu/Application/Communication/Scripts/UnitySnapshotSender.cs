using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Core;
using Grpc.Net.Client;
using Nakatetsu.Application.Simulation;
using Nakatetsu.Track.Interlocking.Management;
using UnityEngine;
using Ctc = Namespace.Protocol.Ctc.V1;
using Interlocking = Nakatetsu.Track.Interlocking;

namespace Nakatetsu.Application.Communication
{
    [DisallowMultipleComponent]
    public sealed class UnitySnapshotSender : MonoBehaviour
    {
        [SerializeField] private ApplicationSimulationController simulation;
        [SerializeField] private TrackInterlockingManagementController interlockingManagement;
        [SerializeField] private string serverAddress = "http://127.0.0.1:50051";
        [SerializeField, Min(0.05f)] private float sendIntervalSeconds = 0.1f;

        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private GrpcChannel channel;
        private Ctc.CtcService.CtcServiceClient client;
        private CancellationTokenSource lifetime;
        private float elapsedSeconds;
        private bool sending;
        private bool connected;
        private StatusCode? lastFailureCode;

        private void Awake()
        {
            if (simulation == null)
                simulation = GetComponent<ApplicationSimulationController>();
        }

        private void OnEnable()
        {
            if (simulation == null || interlockingManagement == null)
            {
                Debug.LogError("[CTC] Simulationと連動管理部を設定してください。", this);
                enabled = false;
                return;
            }
            lifetime = new CancellationTokenSource();
            channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
            {
                HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                DisposeHttpClient = true
            });
            client = new Ctc.CtcService.CtcServiceClient(channel);
            elapsedSeconds = 0f;
            connected = false;
            lastFailureCode = null;
            _ = ReceiveRouteRequestsAsync(client, lifetime.Token);
        }

        private async Task ReceiveRouteRequestsAsync(
            Ctc.CtcService.CtcServiceClient routeClient, CancellationToken token)
        {
            // OnEnableから開始し、他のコンポーネントのAwakeが完了してから受信する。
            await Task.Yield();
            StatusCode? lastRouteFailureCode = null;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var call = routeClient.ConnectRouteControl(cancellationToken: token);
                    while (await call.ResponseStream.MoveNext(token))
                    {
                        token.ThrowIfCancellationRequested();
                        lastRouteFailureCode = null;
                        var request = call.ResponseStream.Current;

                        // Unityのメインスレッドで連動装置に要求を渡す。
                        var result = interlockingManagement.TryRouteRequest(
                            new Interlocking.InterlockingRouteRequest
                            {
                                RequestId = request.RequestId,
                                StationId = request.StationId,
                                RouteId = request.RouteId,
                                Operation = (Interlocking.InterlockingRouteOperation)request.Operation
                            });

                        var response = new Ctc.InterlockingRouteRequestResult
                        {
                            RequestId = result.RequestId,
                            StationId = result.StationId,
                            RouteId = result.RouteId,
                            Accepted = result.Accepted,
                            ErrorCode = (Ctc.InterlockingRouteErrorCode)result.ErrorCode
                        };
                        if (result.RelatedRouteId != null)
                            response.RelatedRouteId = result.RelatedRouteId;
                        if (result.RelatedCircuitId != null)
                            response.RelatedCircuitId = result.RelatedCircuitId;
                        if (result.RelatedTurnoutId != null)
                            response.RelatedTurnoutId = result.RelatedTurnoutId;
                        if (result.Message != null)
                            response.Message = result.Message;

                        await call.RequestStream.WriteAsync(response);
                    }
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (RpcException exception)
                {
                    if (lastRouteFailureCode != exception.StatusCode)
                        Debug.LogWarning($"[CTC] 進路要求の接続を再試行します: {exception.Status}", this);
                    lastRouteFailureCode = exception.StatusCode;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    return;
                }

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async void LateUpdate()
        {
            if (sending || client == null) return;
            elapsedSeconds += Time.unscaledDeltaTime;
            if (elapsedSeconds < sendIntervalSeconds) return;
            elapsedSeconds = 0f;
            sending = true;
            CancellationToken cancellation = lifetime.Token;

            try
            {
                // awaitより前に全項目をコピーし、同じ完了Tickの組を送る。
                Ctc.UnitySnapshotMessage message = CreateSnapshot();
                await client.UpdateSnapshotAsync(message,
                    deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (!connected)
                    Debug.Log($"[CTC] 接続しました: {serverAddress} " +
                        $"(tick={message.Payload.WorldTime.TickCount}, 駅数={message.Payload.Interlocking.StationsById.Count})", this);
                connected = true;
                lastFailureCode = null;
            }
            catch (Exception) when (cancellation.IsCancellationRequested)
            {
                // Play終了やコンポーネント停止によるキャンセル。
            }
            catch (RpcException exception)
            {
                if (lastFailureCode != exception.StatusCode)
                {
                    if (exception.StatusCode == StatusCode.FailedPrecondition)
                        Debug.LogError($"[CTC] 初期化エラー: {exception.Status.Detail}", this);
                    else
                        Debug.LogWarning($"[CTC] 送信できません。再試行します: {exception.Status}", this);
                }
                connected = false;
                lastFailureCode = exception.StatusCode;
            }
            finally
            {
                sending = false;
            }
        }

        private Ctc.UnitySnapshotMessage CreateSnapshot()
        {
            var snapshot = new Ctc.UnitySnapshot
            {
                WorldTime = new Ctc.WorldTimeInformation
                {
                    TickCount = simulation.CompletedTickCount,
                    WorldTimeSeconds = (float)simulation.WorldTimeSeconds,
                    TickDurationSeconds = simulation.TickDurationSeconds,
                    IsPaused = simulation.IsPaused,
                    PlaybackSpeed = simulation.PlaybackSpeed
                },
                Interlocking = new Ctc.InterlockingSnapshot()
            };

            if (simulation.CompletedOccupiedByCircuitId != null)
            {
                snapshot.TrackCircuits = new Ctc.TrackCircuitSnapshot();
                foreach (var pair in simulation.CompletedOccupiedByCircuitId)
                    snapshot.TrackCircuits.OccupiedByCircuitId.Add(pair.Key, pair.Value);
            }

            if (simulation.CompletedTurnoutsById != null)
            {
                snapshot.Turnouts = new Ctc.TurnoutSnapshot();
                foreach (var pair in simulation.CompletedTurnoutsById)
                {
                    snapshot.Turnouts.TurnoutsById.Add(pair.Key, new Ctc.TurnoutState
                    {
                        ActualPosition = (Ctc.TurnoutPosition)pair.Value.ActualPosition,
                        IsMoving = pair.Value.IsMoving
                    });
                }
            }

            if (simulation.CompletedTrainInitialization != null)
            {
                snapshot.TrainInitialization = new Ctc.TrainInitializationSnapshot();
                foreach (var train in simulation.CompletedTrainInitialization)
                {
                    snapshot.TrainInitialization.Trains.Add(new Ctc.TrainInitializationInfo
                    {
                        ConsistId = train.ConsistId,
                        ConsistNumber = train.ConsistNumber,
                        TrainNumber = train.TrainNumber,
                        OperationNumber = train.OperationNumber,
                        ReferenceCircuitId = train.ReferenceCircuitId
                    });
                }
            }

            foreach (var station in simulation.CompletedInterlockingOutput.StationsById)
            {
                var status = new Ctc.InterlockingStationStatus
                {
                    IsInitialized = station.Value.IsInitialized,
                    HasOutput = station.Value.HasOutput
                };
                foreach (var route in station.Value.RoutesById)
                {
                    var value = route.Value;
                    status.RoutesById.Add(route.Key, new Ctc.InterlockingRouteStatus
                    {
                        IsAvailable = value.IsAvailable,
                        IsRouteSet = value.IsRouteSet,
                        PathEstablished = value.PathEstablished,
                        ProceedAllowed = value.ProceedAllowed,
                        RouteLocked = value.RouteLocked,
                        CancelPending = value.CancelPending,
                        ApproachLocked = value.ApproachLocked,
                        ApproachReleaseRemainingSeconds = value.ApproachReleaseRemainingSeconds,
                        OverrunMode = (Ctc.OverrunProtectionMode)value.OverrunMode,
                        OverrunPhase = (Ctc.OverrunProtectionPhase)value.OverrunPhase,
                        OverrunReleaseRemainingSeconds = value.OverrunReleaseRemainingSeconds
                    });
                }
                snapshot.Interlocking.StationsById.Add(station.Key, status);
            }

            return new Ctc.UnitySnapshotMessage
            {
                Header = new Ctc.MessageHeader
                {
                    SchemaVersion = 1,
                    SessionId = sessionId,
                    MessageType = "UnitySnapshot"
                },
                Payload = snapshot
            };
        }

        private void OnDisable()
        {
            client = null;
            lifetime?.Cancel();
            channel?.Dispose();
            lifetime?.Dispose();
        }
    }
}
