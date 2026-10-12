using System;
using System.Threading.Tasks;
using Nakatetsu.Application.Simulation;
using Nakatetsu.Contracts.Communication;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Contracts.World;
using Nakatetsu.Track.Interlocking.Management;
using UnityEngine;

namespace Nakatetsu.Application.Communication
{
    public sealed class UnitySnapshotSender : MonoBehaviour, IServerMessageSender
    {
        private ApplicationSimulationController simulation;
        private UnityCommunicationController communicationController;

        private void Awake()
        {
            communicationController = GetComponentInParent<UnityCommunicationController>();
            simulation = FindAnyObjectByType<ApplicationSimulationController>();
        }

        public Task SendAsync()
        {
            if (simulation == null)
            {
                throw new InvalidOperationException("シーン内にApplicationSimulationControllerが見つかりません。");
            }

            if (communicationController == null)
            {
                throw new InvalidOperationException("親階層にUnityCommunicationControllerが見つかりません。");
            }

            // メインスレッドで完了済みTickの時刻と連動出力をコピーしてから送信する。
            var snapshot = new UnitySnapshot
            {
                WorldTime = new WorldTimeInformation
                {
                    TickCount = simulation.CompletedTickCount,
                    WorldTimeSeconds = (float)simulation.WorldTimeSeconds,
                    TickDurationSeconds = simulation.TickDurationSeconds,
                    IsPaused = simulation.IsPaused,
                    PlaybackSpeed = simulation.PlaybackSpeed
                },
                Interlocking = CreateInterlockingSnapshot(simulation.CompletedInterlockingOutput)
            };

            var message = new Message<UnitySnapshot>
            {
                Header = new MessageHeader
                {
                    SessionId = communicationController.SessionId,
                    MessageType = "UnitySnapshot"
                },
                Payload = snapshot
            };

            var hub = communicationController.GetConnectedHub();
            return hub.UpdateSnapshotAsync(message);
        }

        private static InterlockingSnapshot CreateInterlockingSnapshot(
            TrackInterlockingManagementOutput output)
        {
            var snapshot = new InterlockingSnapshot();

            foreach (var pair in output.StationsById)
            {
                string stationId = pair.Key;
                var station = pair.Value;
                var stationSnapshot = new InterlockingStationStatus
                {
                    IsInitialized = station.IsInitialized,
                    HasOutput = station.HasOutput
                };

                foreach (var routePair in station.RoutesById)
                {
                    string routeId = routePair.Key;
                    var route = routePair.Value;
                    var routeSnapshot = new InterlockingRouteStatus
                    {
                        IsAvailable = route.IsAvailable,
                        IsRouteSet = route.IsRouteSet,
                        PathEstablished = route.PathEstablished,
                        ProceedAllowed = route.ProceedAllowed,
                        RouteLocked = route.RouteLocked,
                        CancelPending = route.CancelPending,
                        ApproachLocked = route.ApproachLocked,
                        ApproachReleaseRemainingSeconds = route.ApproachReleaseRemainingSeconds,
                        OverrunMode = (OverrunProtectionMode)route.OverrunMode,
                        OverrunPhase = (OverrunProtectionPhase)route.OverrunPhase,
                        OverrunReleaseRemainingSeconds = route.OverrunReleaseRemainingSeconds
                    };

                    stationSnapshot.RoutesById.Add(routeId, routeSnapshot);
                }

                snapshot.StationsById.Add(stationId, stationSnapshot);
            }

            return snapshot;
        }
    }
}
