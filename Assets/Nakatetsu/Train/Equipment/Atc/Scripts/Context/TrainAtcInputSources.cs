using System.Collections.Generic;

namespace Nakatetsu.Train.Equipment.Atc
{
    public interface ITrainAtcCabInputSource
    {
        bool TryReadCabInput(out TrainAtcCabInput input);
    }

    public interface ITrainAtcDoorInputSource
    {
        bool TryReadDoorState(out bool areAllDoorsClosed);
        bool TryReadOpeningOperationRevision(out int revision);
    }

    public interface ITrainAtcMassInputSource
    {
        bool TryReadCarInputs(List<TrainAtcCarInput> cars);
    }

    public interface ITrainAtcBrakeSettingsInputSource
    {
        bool TryReadBrakeSettings(TrainAtcBrakeSettingsInput input);
    }
}
