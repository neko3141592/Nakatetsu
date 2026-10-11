using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Nakatetsu.Application.Communication
{
    public sealed class ServerMessageSenderController : MonoBehaviour
    {
        private UnityCommunicationController communicationController;
        [SerializeField, Min(0.1f)] private float sendIntervalSeconds = 0.1f;

        private IServerMessageSender[] senders;
        private float elapsedTimeSeconds;
        private bool isSending;

        private void Awake()
        {
            communicationController = GetComponentInParent<UnityCommunicationController>();
            senders = GetComponentsInChildren<IServerMessageSender>();

            if (communicationController == null)
            {
                Debug.LogError(
                    "親階層にUnityCommunicationControllerが見つかりません。", this);
                enabled = false;
            }
        }

        private async void Update()
        {
            if (!communicationController.IsConnected)
            {
                elapsedTimeSeconds = 0f;
                return;
            }

            elapsedTimeSeconds += Time.unscaledDeltaTime;
            if (isSending || elapsedTimeSeconds < sendIntervalSeconds)
            {
                return;
            }

            elapsedTimeSeconds = 0f;

            isSending = true;
            try
            {
                await SendAllAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                isSending = false;
            }
        }

        public Task SendAllAsync()
        {
            var tasks = new Task[senders.Length];
            for (int i = 0; i < senders.Length; i++)
            {
                tasks[i] = senders[i].SendAsync();
            }

            return Task.WhenAll(tasks);
        }
    }
}
