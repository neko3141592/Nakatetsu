using System;
using System.Threading.Tasks;
using Nakatetsu.Contracts.Communication;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Track.Interlocking.Management;
using UnityEngine;


namespace Nakatetsu.Application.Communication
{
    public sealed class InterlockingSnapshotSender : MonoBehaviour, IServerMessageSender
    {
        private TrackInterlockingManagementController interlocking;
        private UnityCommunicationController communicationController;

        private void Awake()
        {
            communicationController = GetComponentInParent<UnityCommunicationController>();
            interlocking = FindAnyObjectByType<TrackInterlockingManagementController>();
        }

        public Task SendAsync()
        {
            if (interlocking == null)
            {
                throw new InvalidOperationException("シーン内にTrackInterlockingManagementControllerが見つかりません。");
            }
            if (communicationController == null)
            {
                throw new InvalidOperationException("親階層にUnityCommunicationControllerが見つかりません。");
            }

            InterlockingSnapshot snapshot = CreateSnapshot(interlocking.Context.Output);

            var message = new Message<InterlockingSnapshot>
            {
                Header = new MessageHeader
                {
                    SessionId = communicationController.SessionId,
                    MessageType = "Interlocking"
                },
                Payload = snapshot
            };


            var hub = communicationController.GetConnectedHub();
            return hub.UpdateInterlockingSnapshotAsync(message);



        }

        private static InterlockingSnapshot CreateSnapshot(
            TrackInterlockingManagementOutput output)
        {
            InterlockingSnapshot snapshot = new();


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
