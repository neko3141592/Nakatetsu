using Nakatetsu.Contracts.Communication;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Contracts.World;

namespace Nakatetsu.TrafficControl.Server.Communication.Unity
{
    public sealed class UnitySnapshotStore
    {
        private readonly object syncRoot = new();
        private Message<UnitySnapshot>? latest;

        public void Update(Message<UnitySnapshot> message)
        {
            lock (syncRoot)
            {
                latest = Copy(message);
            }
        }

        public Message<UnitySnapshot>? GetLatest()
        {
            lock (syncRoot)
            {
                return latest is null ? null : Copy(latest);
            }
        }

        internal static Message<UnitySnapshot> Copy(Message<UnitySnapshot> message)
        {
            return new Message<UnitySnapshot>
            {
                Header = new MessageHeader
                {
                    SchemaVersion = message.Header.SchemaVersion,
                    SessionId = message.Header.SessionId,
                    MessageType = message.Header.MessageType
                },
                Payload = new UnitySnapshot
                {
                    WorldTime = CopyWorldTime(message.Payload.WorldTime),
                    Interlocking = CopyInterlocking(message.Payload.Interlocking)
                }
            };
        }

        private static WorldTimeInformation CopyWorldTime(WorldTimeInformation time)
        {
            return new WorldTimeInformation
            {
                TickCount = time.TickCount,
                WorldTimeSeconds = time.WorldTimeSeconds,
                TickDurationSeconds = time.TickDurationSeconds,
                IsPaused = time.IsPaused,
                PlaybackSpeed = time.PlaybackSpeed
            };
        }

        private static InterlockingSnapshot CopyInterlocking(InterlockingSnapshot snapshot)
        {
            var copy = new InterlockingSnapshot();

            foreach (var stationPair in snapshot.StationsById)
            {
                var station = stationPair.Value;
                var stationCopy = new InterlockingStationStatus
                {
                    IsInitialized = station.IsInitialized,
                    HasOutput = station.HasOutput
                };

                foreach (var routePair in station.RoutesById)
                {
                    var route = routePair.Value;
                    stationCopy.RoutesById.Add(
                        routePair.Key,
                        new InterlockingRouteStatus
                        {
                            IsAvailable = route.IsAvailable,
                            IsRouteSet = route.IsRouteSet,
                            PathEstablished = route.PathEstablished,
                            ProceedAllowed = route.ProceedAllowed,
                            RouteLocked = route.RouteLocked,
                            CancelPending = route.CancelPending,
                            ApproachLocked = route.ApproachLocked,
                            ApproachReleaseRemainingSeconds = route.ApproachReleaseRemainingSeconds,
                            OverrunMode = route.OverrunMode,
                            OverrunPhase = route.OverrunPhase,
                            OverrunReleaseRemainingSeconds = route.OverrunReleaseRemainingSeconds
                        });
                }

                copy.StationsById.Add(stationPair.Key, stationCopy);
            }

            return copy;
        }
    }
}
