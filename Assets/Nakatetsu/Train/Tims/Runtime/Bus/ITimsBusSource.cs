namespace Nakatetsu.Train.Tims.Bus {
    public interface ITimsBusSource
    {
        float TransmissionIntervalSeconds { get; }

        void WriteTimsBus(TimsBusState localBus);
    }
}