namespace Nakatetsu.Track.NewAtc
{
    public sealed class TrackAtcState
    {
        public TrackAtcValidationState validation = new();
        public TrackAtcPathState path = new();
        public TrackAtcProtectionModeState protectionMode = new();
        public TrackAtcTelegramState telegram = new();

        // 親Logicが各工程の確定結果から集約する、今回の全体の正常性。
        public bool isAtcHealthy;
    }
}
