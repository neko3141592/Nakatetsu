namespace Nakatetsu.Train.Tims.Bus {
    public interface ITimsBusSource
    {
        int AssignedCarIndex { get; }

        void WriteTimsBus(TimsBusState localBus);
    }
}
