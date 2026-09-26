using Nakatetsu.Train.Equipment.Operation;

namespace Nakatetsu.Train.Equipment.Safety.Eb
{
    public struct EbMasterControllerInput
    {
        public int powerPosition;
        public int brakePosition;
        public ReverserPosition reverserPosition;
        public bool isInputEnabled;
        public bool isActiveCab;

        public float speedMps;
    }

    public interface IEbMasterControllerInputSource
    {
        bool TryReadMasterControllerInput(out EbMasterControllerInput input);
    }

    public sealed class EbDeviceInput
    {
        public bool resetRequested;
        public bool hasMasterControllerState;
        public EbMasterControllerInput masterController;
    }

    public sealed class EbDeviceSettings
    {
        public float warningDelaySeconds = 60f;
        public float warningDurationSeconds = 5f;
        public float activationSpeedMps = 5f / 3.6f;
    }

    public sealed class EbDeviceState
    {
        public bool isEmergencyBrakeLatched;
        public bool hasPreviousMasterControllerState;
        public EbMasterControllerInput previousMasterController;
        public float inactivitySeconds;
    }

    public sealed class EbDeviceOutput
    {
        public bool isBuzzerRequested;
        public bool isEmergencyBrakeRequested;
        public float inactivitySeconds;
        public float remainingSeconds;
    }

    public sealed class EbDeviceContext
    {
        public EbDeviceInput Input { get; } = new();
        public EbDeviceSettings Settings { get; } = new();
        public EbDeviceState State { get; } = new();
        public EbDeviceOutput Output { get; } = new();
    }
}
