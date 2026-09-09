namespace Nakatetsu.Train.Tims.Bus {
    public interface ITimsBusSource
    {
        int AssignedCarIndex { get; }
        float TransmissionIntervalSeconds { get; }

        void WriteTimsBus(TimsBusState localBus);
    }
}
