using System;
using System.Threading.Tasks;
using Nakatetsu.Application.Simulation;
using Nakatetsu.Contracts.Communication;
using Nakatetsu.Contracts.World;
using UnityEngine;

namespace Nakatetsu.Application.Communication
{
    public sealed class WorldTimeSender : MonoBehaviour, IServerMessageSender
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

            var message = new Message<WorldTimeInformation>
            {
                Header = new MessageHeader
                {
                    SessionId = communicationController.SessionId,
                    MessageType = "WorldTime"
                },
                Payload = new WorldTimeInformation
                {
                    TickCount = simulation.CompletedTickCount,
                    WorldTimeSeconds = (float)simulation.WorldTimeSeconds,
                    TickDurationSeconds = simulation.TickDurationSeconds,
                    IsPaused = simulation.IsPaused,
                    PlaybackSpeed = simulation.PlaybackSpeed
                }
            };

            var hub = communicationController.GetConnectedHub();
            return hub.UpdateWorldTimeAsync(message);
        }
    }
}
